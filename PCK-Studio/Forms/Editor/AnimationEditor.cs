﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using MetroFramework.Forms;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using OMI.Formats.Pck;

using PckStudio.Forms.Additional_Popups.Animation;
using PckStudio.Extensions;
using PckStudio.Properties;
using PckStudio.Internal;
using PckStudio.Internal.Json;
using PckStudio.Helper;

namespace PckStudio.Forms.Editor
{
    public partial class AnimationEditor : MetroForm
	{
		private Animation _animation;

		private string _tileName = string.Empty;

        private bool IsSpecialTile(string tileName)
        {
			return tileName == "clock" || tileName == "compass";
        }

		private AnimationEditor()
		{
            InitializeComponent();
            toolStripSeparator1.Visible = saveToolStripMenuItem1.Visible = !Settings.Default.AutoSaveChanges;
        }

        internal AnimationEditor(Animation animation, string name)
			: this()
		{
			_ = animation ?? throw new ArgumentNullException(nameof(animation));
			_animation = animation;
			_tileName = name;
        }

        internal AnimationEditor(Animation animation, string name, Color blendColor)
			: this(animation, name)
        {
			animationPictureBox.UseBlendColor = true;
			animationPictureBox.BlendColor = blendColor;
		}

        private void AnimationEditor_Load(object sender, EventArgs e)
        {
            bulkAnimationSpeedToolStripMenuItem.Enabled =
            importToolStripMenuItem.Enabled =
            exportAsToolStripMenuItem.Enabled =
            InterpolationCheckbox.Visible = !IsSpecialTile(_tileName);

            SetTileLabel();
            LoadAnimationTreeView();
        }

        private void LoadAnimationTreeView()
		{
			if (_animation is null)
			{
                AnimationStartStopBtn.Enabled = false;
                return;
			}
            AnimationStartStopBtn.Enabled = true;
            InterpolationCheckbox.Checked = _animation.Interpolate;
			frameTreeView.Nodes.Clear();
			TextureIcons.Images.Clear();
			TextureIcons.Images.AddRange(_animation.GetTextures().ToArray());
			foreach (var frame in _animation.GetFrames())
			{
				var imageIndex = _animation.GetTextureIndex(frame.Texture);
				frameTreeView.Nodes.Add(new TreeNode($"for {frame.Ticks} ticks")
				{
					ImageIndex = imageIndex,
					SelectedImageIndex = imageIndex,
				});
            }
			if (_animation.FrameCount > 0)
			{
				animationPictureBox.SelectFrame(_animation, 0);
			}
		}

		private void frameTreeView_AfterSelect(object sender, TreeViewEventArgs e)
		{
			if (animationPictureBox.IsPlaying)
                AnimationStartStopBtn.Text = "Play Animation";
            animationPictureBox.SelectFrame(_animation, frameTreeView.SelectedNode.Index);
		}

		private void AnimationStartStopBtn_Click(object sender, EventArgs e)
		{
			if (animationPictureBox.IsPlaying)
			{
				animationPictureBox.Stop();
				AnimationStartStopBtn.Text = "Play Animation";
				return;
			}
            if (_animation.FrameCount > 1)
			{
                animationPictureBox.Start(_animation);
				AnimationStartStopBtn.Text = "Stop Animation";
			}
		}

		private void frameTreeView_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyData == Keys.Delete)
				removeFrameToolStripMenuItem_Click(sender, e);
		}

		private TreeNode FindNodeByName(TreeNode treeNode, string name)
		{
			foreach (TreeNode node in treeNode.Nodes)
			{
				if (node.Text.ToLower() == name.ToLower()) return node;
				return FindNodeByName(node, name);
			}
			return null;
		}

		private void saveToolStripMenuItem1_Click(object sender, EventArgs e)
		{
			if (!IsSpecialTile(_tileName) && _animation is not null)
			{
				DialogResult = DialogResult.OK;
				return;
			}
			DialogResult = DialogResult.Cancel;
		}

		// Most of the code below is modified code from this link: https://docs.microsoft.com/en-us/dotnet/api/system.windows.forms.treeview.itemdrag?view=windowsdesktop-6.0
		// - MattNL

		private void frameTreeView_ItemDrag(object sender, ItemDragEventArgs e)
		{
			// Move the dragged node when the left mouse button is used.
			if (e.Button == MouseButtons.Left)
			{
				DoDragDrop(e.Item, DragDropEffects.Move);
			}
		}

		// Set the target drop effect to the effect 
		// specified in the ItemDrag event handler.
		private void frameTreeView_DragEnter(object sender, DragEventArgs e)
		{
			e.Effect = e.AllowedEffect;
		}

		// Select the node under the mouse pointer to indicate the 
		// expected drop location.
		private void frameTreeView_DragOver(object sender, DragEventArgs e)
		{
			// Retrieve the client coordinates of the mouse position.
			Point targetPoint = frameTreeView.PointToClient(new Point(e.X, e.Y));

			// Select the node at the mouse position.
			frameTreeView.SelectedNode = frameTreeView.GetNodeAt(targetPoint);
		}

		private void frameTreeView_DragDrop(object sender, DragEventArgs e)
		{
			// Retrieve the client coordinates of the drop location.
			Point targetPoint = frameTreeView.PointToClient(new Point(e.X, e.Y));

			// Retrieve the node at the drop location.
			TreeNode targetNode = frameTreeView.GetNodeAt(targetPoint);

			// Retrieve the node that was dragged.
			TreeNode draggedNode = (TreeNode)e.Data.GetData(typeof(TreeNode));

			// Confirm that the node at the drop location is not 
			// the dragged node or a descendant of the dragged node.
			if (targetNode == null)
			{
				draggedNode.Remove();
				frameTreeView.Nodes.Add(draggedNode);
			}
			else if (!draggedNode.Equals(targetNode) && !ContainsNode(draggedNode, targetNode))
			{
				// If it is a move operation, remove the node from its current 
				// location and add it to the node at the drop location.

				if (e.Effect == DragDropEffects.Move)
				{
					int draggedIndex = draggedNode.Index;
					int targetIndex = targetNode.Index;
					_animation.SwapFrames(draggedIndex, targetIndex);
					LoadAnimationTreeView();
				}
			}
		}

		// Determine whether one node is a parent 
		// or ancestor of a second node.
		private bool ContainsNode(TreeNode node1, TreeNode node2)
		{
			// Check the parent node of the second node.
			if (node2.Parent == null) return false;
			if (node2.Parent.Equals(node1)) return true;

			// If the parent node is not null or equal to the first node, 
			// call the ContainsNode method recursively using the parent of 
			// the second node.
			return ContainsNode(node1, node2.Parent);
		}

		private void treeView1_doubleClick(object sender, EventArgs e)
		{
            var frame = _animation.GetFrame(frameTreeView.SelectedNode.Index);
            using FrameEditor diag = new FrameEditor(frame.Ticks, _animation.GetTextureIndex(frame.Texture), TextureIcons);
			if (diag.ShowDialog(this) == DialogResult.OK)
            {
				/* Found a bug here. When passing the frame variable,
				 * it would replace the first instance of that frame and time
				 * rather than the actual frame that was clicked.
				 * I've just switched to passing the index to fix this for now.
				 * - Matt
				*/

                _animation.SetFrame(frameTreeView.SelectedNode.Index, diag.FrameTextureIndex, diag.FrameTime);
                LoadAnimationTreeView();
            }
        }

		private void addFrameToolStripMenuItem_Click(object sender, EventArgs e)
		{
            using FrameEditor diag = new FrameEditor(TextureIcons);
			diag.SaveBtn.Text = "Add";
			if (diag.ShowDialog(this) == DialogResult.OK)
			{
                _animation.AddFrame(diag.FrameTextureIndex, IsSpecialTile(_tileName) ? Animation.MinimumFrameTime : diag.FrameTime);
                LoadAnimationTreeView();
			}
		}

		private void removeFrameToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (frameTreeView.SelectedNode is TreeNode t &&
				_animation.RemoveFrame(t.Index))
				frameTreeView.SelectedNode.Remove();

		}

		private void bulkAnimationSpeedToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetBulkSpeed diag = new SetBulkSpeed();
			if (diag.ShowDialog(this) == DialogResult.OK)
			{
				if (animationPictureBox.IsPlaying)
					animationPictureBox.Stop();
                _animation.SetFrameTicks(diag.Ticks);
				LoadAnimationTreeView();
			}
			diag.Dispose();
		}

		// Reworked import tool with new Animation classes by Miku
		private void importJavaAnimationToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (MessageBox.Show(
				"This feature will replace the existing animation data. " +
				"It might fail if the selected animation script is invalid. " +
				"Are you sure that you want to continue?",
				"Warning",
				MessageBoxButtons.YesNo) == DialogResult.No)
				return;

            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Title = "Please select a valid Minecaft: Java Edition animation script",
                // It's marked as .png.mcmeta just in case
                // some weirdo tries to pass a pack.mcmeta or something
                // -MattNL
                Filter = "Animation Scripts (*.mcmeta)|*.png.mcmeta"
            };
            if (fileDialog.ShowDialog(this) != DialogResult.OK) return;
			Debug.WriteLine("Selected Animation Script: " + fileDialog.FileName);

			string textureFile = fileDialog.FileName.Substring(0, fileDialog.FileName.Length - ".mcmeta".Length);
            if (!File.Exists(textureFile))
			{
				MessageBox.Show(textureFile + " was not found", "Texture not found");
				return;
			}
            try
			{
				var img = Image.FromFile(textureFile);
				JObject mcmeta = JObject.Parse(File.ReadAllText(fileDialog.FileName));
                Animation javaAnimation = AnimationHelper.GetAnimationFromJavaAnimation(mcmeta, img);
				javaAnimation.Category = _animation.Category;
				_animation = javaAnimation;
				LoadAnimationTreeView();
			}
			catch (JsonException j_ex)
			{
				MessageBox.Show(j_ex.Message, "Invalid animation");
				return;
			}
		}

		private void changeTileToolStripMenuItem_Click(object sender, EventArgs e)
		{
            using (ChangeTile diag = new ChangeTile())
				{
				if (diag.ShowDialog(this) != DialogResult.OK)
					return;
				
					Debug.WriteLine(diag.SelectedTile);
                _animation.Category = diag.Category;
				_tileName = diag.SelectedTile;

					bulkAnimationSpeedToolStripMenuItem.Enabled = 
					importToolStripMenuItem.Enabled = 
					exportAsToolStripMenuItem.Enabled = 
				InterpolationCheckbox.Visible = !IsSpecialTile(_tileName);

					SetTileLabel();
				}
        }

        private void SetTileLabel()
        {
			var textureInfos = _animation.Category switch
			{
				Animation.AnimationCategory.Blocks => Tiles.BlockTileInfos,
				Animation.AnimationCategory.Items => Tiles.ItemTileInfos,
				_ => throw new ArgumentOutOfRangeException(_animation.Category.ToString())
			};
			tileLabel.Text = textureInfos.FirstOrDefault(p => p.InternalName == _tileName)?.DisplayName ?? _tileName;

            //switch (MessageBox.Show(this, 
			//	$"{TileName} is not a valid tile for animation, and will not play in game. Would you like to choose a new tile?", 
			//	"Not a valid tile", 
			//	MessageBoxButtons.YesNo))
			//{
			//	case DialogResult.Yes:
			//		changeTileToolStripMenuItem_Click(null, EventArgs.Empty);
			//		break;
			//	default:
			//		DialogResult = DialogResult.Abort;
			//		break;
			//}
		}

        private void exportJavaAnimationToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SaveFileDialog fileDialog = new SaveFileDialog();
			fileDialog.Title = "Please choose where you want to save your new animation";
			fileDialog.Filter = "Animation Scripts (*.mcmeta)|*.png.mcmeta";
			if (fileDialog.ShowDialog(this) == DialogResult.OK)
            {
                JObject mcmeta = _animation.ConvertToJavaAnimation();
                string jsondata = JsonConvert.SerializeObject(mcmeta, Formatting.Indented);
                string filename = fileDialog.FileName;
                File.WriteAllText(filename, jsondata);
                var finalTexture = _animation.BuildTexture();
				// removes ".mcmeta" from filename
				string texturePath = Path.Combine(Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(filename));
                finalTexture.Save(texturePath);
                MessageBox.Show("Animation was successfully exported as " + Path.GetFileName(filename), "Export successful!");
            }
        }

        private void howToInterpolation_Click(object sender, EventArgs e)
		{
			MessageBox.Show("The Interpolation effect is when the animtion smoothly translates between the frames instead of simply displaying the next one. This can be seen with some vanilla Minecraft textures such as Magma and Prismarine.", "Interpolation");
		}

		private void editorControlsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			MessageBox.Show("Simply drag and drop frames in the tree to rearrange your animation.\n\n" +
				"You can also preview your animation at any time by simply pressing the button under the animation display.", "Editor Controls");
		}

		private void setBulkSpeedToolStripMenuItem_Click(object sender, EventArgs e)
		{
			MessageBox.Show("You can edit the frame and its speed by double clicking a frame in the tree. If you'd like to change the entire animation's speed, you can do so with the \"Set Bulk Animation Speed\" button in the \"Tools\" tab", "How to use Bulk Animation tool");
		}

		private void javaAnimationSupportToolStripMenuItem_Click(object sender, EventArgs e)
		{
			MessageBox.Show("You can import any valid Java Edition tile animations into your pck by opening an mcmeta.\n\n" +
				"You can also export your animation as an Java Edition tile animation. It will also export the actual texture in the same spot.", "Java Edition Support");
		}

		private void InterpolationCheckbox_CheckedChanged(object sender, EventArgs e)
		{
			if (_animation is not null)
				_animation.Interpolate = InterpolationCheckbox.Checked;
		}

        private void AnimationEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
			if (animationPictureBox.IsPlaying)
			{
                animationPictureBox.Stop();
			}
			if (Settings.Default.AutoSaveChanges)
			{
				saveToolStripMenuItem1_Click(sender, EventArgs.Empty);
			}
        }

        private void importGifToolStripMenuItem_Click(object sender, EventArgs e)
        {
			OpenFileDialog fileDialog = new OpenFileDialog()
			{
				Filter = "GIF file|*.gif"
			};
			if (fileDialog.ShowDialog(this) != DialogResult.OK)
				return;

			var gif = Image.FromFile(fileDialog.FileName);
			if (!gif.RawFormat.Equals(ImageFormat.Gif))
			{
				MessageBox.Show("Selected file is not a gif", "Invalid file", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

            FrameDimension dimension = new FrameDimension(gif.FrameDimensionsList[0]);
            int frameCount = gif.GetFrameCount(dimension);

			var textures = new List<Image>(frameCount);

			for (int i = 0; i < frameCount; i++)
			{
				gif.SelectActiveFrame(dimension, i);
				textures.Add(new Bitmap(gif));
			}
			_animation = new Animation(textures, string.Empty);
			LoadAnimationTreeView();
        }

        private void importAnimationTextureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog()
            {
                Filter = "PNG Files | *.png",
                Title = "Select a PNG File",
            };
            if (ofd.ShowDialog() != DialogResult.OK)
                return;
            Image img = Image.FromFile(ofd.FileName);
            var textures = img.Split(ImageLayoutDirection.Vertical);
			_animation = new Animation(textures, string.Empty);
			LoadAnimationTreeView();
        }

        //[System.Runtime.InteropServices.DllImport("gdi32.dll")]
        //public static extern bool DeleteObject(IntPtr hObject);

        private void gifToolStripMenuItem_Click(object sender, EventArgs e)
        {
			MessageBox.Show(this, "This feature is still under development", "Coming soon");
			return;

			// TODO
			//var fileDialog = new SaveFileDialog()
            //{
            //    Filter = "GIF file|*.gif"
            //};
            //if (fileDialog.ShowDialog(this) != DialogResult.OK)
            //    return;

			//GifBitmapEncoder gifBitmapEncoder = new GifBitmapEncoder();

			//foreach (Bitmap texture in currentAnimation.GetTextures())
			//{
			//	var bmp = texture.GetHbitmap();
			//	var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
			//		bmp,
			//		IntPtr.Zero,
			//		System.Windows.Int32Rect.Empty,
			//		BitmapSizeOptions.FromWidthAndHeight(texture.Width, texture.Height));
			//	gifBitmapEncoder.Frames.Add(BitmapFrame.Create(src));
			//	DeleteObject(bmp); // recommended, handle memory leak
			//}

			//using (var fs = fileDialog.OpenFile())
			//{
			//	gifBitmapEncoder.Save(fs);
			//}
		}

		private void frameTimeandTicksToolStripMenuItem_Click(object sender, EventArgs e)
		{
			MessageBox.Show(this, "The frame time is the time that the current frame is displayed for. This unit is measured in ticks. " +
				"All time related functions in Minecraft use ticks, notably redstone repeaters. There are 20 ticks in 1 second, so " +
				"1 tick is 1/20 of a second. To find how long your frame is, divide the frame time by 20", "Frame Time and Ticks");
		}
    }
}
