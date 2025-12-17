using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ActEditor.ApplicationConfiguration;
using ActImaging;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.PalFormat;
using GRF.Image;
using GRF.IO;
using GRF.Threading;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;
using Utilities;
using Utilities.Controls;
using ActAction = GRF.FileFormats.ActFormat.Action;

namespace ActEditor.Tools.PaletteGenerator {
	/// <summary>
	/// Interaction logic for PaletteGeneratorWindow.xaml
	/// </summary>
	public partial class PaletteGeneratorWindow : TkWindow {
		private Pal _currentPalette;
		private readonly PaletteGeneratorEngine _engine = new PaletteGeneratorEngine();
		private readonly Act _sourceAct;
		private Pal _previewPalette; // Cópia da paleta para preview
		private bool _isUpdatingPreview = false; // Flag para evitar loops
		private bool _isSyncingHueRange = false; // Flag para evitar loops na sincronização de hue range

		public PaletteGeneratorWindow(Act act)
			: base("Palette Generator", "pal.png", SizeToContent.Manual, ResizeMode.CanResize) {
			if (act == null || act.Sprite == null || act.Sprite.Palette == null) {
				throw new Exception("O ACT atual não possui paleta. Abra um ACT com paleta primeiro.");
			}

			_sourceAct = act;
			InitializeComponent();
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			ShowInTaskbar = true;

			Loaded += _window_Loaded;
		}

		private void _window_Loaded(object sender, RoutedEventArgs e) {
			// Load palette from ACT
			try {
				byte[] paletteData = new byte[1024];
				Buffer.BlockCopy(_sourceAct.Sprite.Palette.BytePalette, 0, paletteData, 0, 1024);
				_currentPalette = new Pal(paletteData);
				_previewPalette = new Pal(paletteData);
				_paletteSelector.SetPalette(_currentPalette);
				_paletteSelector.SelectionChanged += _paletteSelector_SelectionChanged;
				
				// Update ACT info label if available
				if (!String.IsNullOrEmpty(_sourceAct.LoadedPath)) {
					string actName = Path.GetFileName(_sourceAct.LoadedPath);
					_tbActInfo.Text = String.Format("Source: {0}", actName);
				}
				else {
					_tbActInfo.Text = "Source: Current ACT";
				}
				
				_tbStatus.Text = "Palette loaded from ACT successfully";
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				_tbStatus.Text = "Error loading palette from ACT";
			}

			// Populate class ComboBox
			PopulateClassComboBox();
			
			LoadConfiguration();
			
			// Initialize visibility based on current mode selection
			if (_rbModeHsv.IsChecked == true) {
				_gbHsvMode.Visibility = Visibility.Visible;
				_gbColorizeMode.Visibility = Visibility.Collapsed;
				_gbGrayscaleMode.Visibility = Visibility.Collapsed;
			}
			else if (_rbModeColorize.IsChecked == true) {
				_gbHsvMode.Visibility = Visibility.Collapsed;
				_gbColorizeMode.Visibility = Visibility.Visible;
				_gbGrayscaleMode.Visibility = Visibility.Collapsed;
			}
			else if (_rbModeGrayscale.IsChecked == true) {
				_gbHsvMode.Visibility = Visibility.Collapsed;
				_gbColorizeMode.Visibility = Visibility.Collapsed;
				_gbGrayscaleMode.Visibility = Visibility.Visible;
			}

			// Update costume number field visibility
			UpdateCostumeNumberVisibility();

			// Populate preview action and frame ComboBoxes
			PopulatePreviewComboBoxes();
			
			// Connect text change events for debounced preview updates
			ConnectParameterEvents();
			
			// Connect hue range synchronization events
			ConnectHueRangeEvents();
			
			// Initial preview update
			UpdatePreview();
		}

		private void PopulateClassComboBox() {
			_cbClass.ItemsSource = RagnarokClasses.GetAllClasses();
			_cbClass.DisplayMemberPath = "DisplayName";
			_cbClass.SelectedValuePath = "Code";
		}

		private void UpdateCostumeNumberVisibility() {
			if (_rbSpriteTypeCostume != null && _tbCostumeNumber != null) {
				_tbCostumeNumber.IsEnabled = _rbSpriteTypeCostume.IsChecked == true;
			}
		}

		private void _rbSpriteType_Checked(object sender, RoutedEventArgs e) {
			UpdateCostumeNumberVisibility();
		}

		private void PopulatePreviewComboBoxes() {
			_cbPreviewAction.Items.Clear();
			_cbPreviewFrame.Items.Clear();

			if (_sourceAct == null || _sourceAct.NumberOfActions == 0) {
				return;
			}

			// Populate actions
			for (int i = 0; i < _sourceAct.NumberOfActions; i++) {
				_cbPreviewAction.Items.Add(String.Format("Action {0}", i));
			}

			if (_cbPreviewAction.Items.Count > 0) {
				_cbPreviewAction.SelectedIndex = 0;
			}
		}

		private void PopulatePreviewFrames(int actionIndex) {
			_cbPreviewFrame.Items.Clear();

			if (_sourceAct == null || actionIndex < 0 || actionIndex >= _sourceAct.NumberOfActions) {
				return;
			}

			ActAction action = _sourceAct[actionIndex];
			for (int i = 0; i < action.NumberOfFrames; i++) {
				_cbPreviewFrame.Items.Add(String.Format("Frame {0}", i));
			}

			if (_cbPreviewFrame.Items.Count > 0) {
				_cbPreviewFrame.SelectedIndex = 0;
			}
		}

		private void ConnectParameterEvents() {
			// Parameter events removed - preview now updates only when Apply button is clicked
		}

		private void ConnectHueRangeEvents() {
			// Connect events for HSV Standard mode
			_tbHueRange.TextChanged += _tbHueRange_TextChanged;
			_tbHueMin.TextChanged += _tbHueMin_TextChanged;
			_tbHueMax.TextChanged += _tbHueMax_TextChanged;
			
			// Connect events for Colorize mode
			_tbColorizeHueRange.TextChanged += _tbColorizeHueRange_TextChanged;
			_tbColorizeHueMin.TextChanged += _tbColorizeHueMin_TextChanged;
			_tbColorizeHueMax.TextChanged += _tbColorizeHueMax_TextChanged;
		}

		private void _tbHueRange_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
			if (_isSyncingHueRange) return;

			try {
				double range = ParseDouble(_tbHueRange.Text, 360);
				double min = ParseDouble(_tbHueMin.Text, 0);
				double max = min + range;

				// Clamp max to valid range (0-360)
				if (max > 360) max = 360;
				if (max < 0) max = 0;

				_isSyncingHueRange = true;
				_tbHueMax.Text = max.ToString(CultureInfo.InvariantCulture);
				_isSyncingHueRange = false;
			}
			catch {
				_isSyncingHueRange = false;
			}
		}

		private void _tbHueMin_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
			if (_isSyncingHueRange) return;

			try {
				double min = ParseDouble(_tbHueMin.Text, 0);
				double max = ParseDouble(_tbHueMax.Text, 360);
				double range = max - min;

				_isSyncingHueRange = true;
				_tbHueRange.Text = range.ToString(CultureInfo.InvariantCulture);
				_isSyncingHueRange = false;
			}
			catch {
				_isSyncingHueRange = false;
			}
		}

		private void _tbHueMax_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
			if (_isSyncingHueRange) return;

			try {
				double min = ParseDouble(_tbHueMin.Text, 0);
				double max = ParseDouble(_tbHueMax.Text, 360);
				double range = max - min;

				_isSyncingHueRange = true;
				_tbHueRange.Text = range.ToString(CultureInfo.InvariantCulture);
				_isSyncingHueRange = false;
			}
			catch {
				_isSyncingHueRange = false;
			}
		}

		// Colorize mode hue range synchronization
		private void _tbColorizeHueRange_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
			if (_isSyncingHueRange) return;

			try {
				double range = ParseDouble(_tbColorizeHueRange.Text, 360);
				double min = ParseDouble(_tbColorizeHueMin.Text, 0);
				double max = min + range;

				// Clamp max to valid range (0-360)
				if (max > 360) max = 360;
				if (max < 0) max = 0;

				_isSyncingHueRange = true;
				_tbColorizeHueMax.Text = max.ToString(CultureInfo.InvariantCulture);
				_isSyncingHueRange = false;
			}
			catch {
				_isSyncingHueRange = false;
			}
		}

		private void _tbColorizeHueMin_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
			if (_isSyncingHueRange) return;

			try {
				double min = ParseDouble(_tbColorizeHueMin.Text, 0);
				double max = ParseDouble(_tbColorizeHueMax.Text, 360);
				double range = max - min;

				_isSyncingHueRange = true;
				_tbColorizeHueRange.Text = range.ToString(CultureInfo.InvariantCulture);
				_isSyncingHueRange = false;
			}
			catch {
				_isSyncingHueRange = false;
			}
		}

		private void _tbColorizeHueMax_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
			if (_isSyncingHueRange) return;

			try {
				double min = ParseDouble(_tbColorizeHueMin.Text, 0);
				double max = ParseDouble(_tbColorizeHueMax.Text, 360);
				double range = max - min;

				_isSyncingHueRange = true;
				_tbColorizeHueRange.Text = range.ToString(CultureInfo.InvariantCulture);
				_isSyncingHueRange = false;
			}
			catch {
				_isSyncingHueRange = false;
			}
		}

		private void _cbPreviewAction_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_cbPreviewAction.SelectedIndex >= 0) {
				PopulatePreviewFrames(_cbPreviewAction.SelectedIndex);
				// Preview update removed - now requires clicking Apply button
			}
		}

		private void _cbPreviewFrame_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			// Preview update removed - now requires clicking Apply button
		}

		private void _btnApplyPreview_Click(object sender, RoutedEventArgs e) {
			UpdatePreview();
		}

		private string FormatSelectedIndices(int[] indices) {
			if (indices == null || indices.Length == 0) {
				return "";
			}

			// Sort indices
			int[] sorted = indices.OrderBy(i => i).ToArray();
			List<string> ranges = new List<string>();

			int start = sorted[0];
			int end = sorted[0];

			for (int i = 1; i < sorted.Length; i++) {
				if (sorted[i] == end + 1) {
					// Consecutive, extend range
					end = sorted[i];
				}
				else {
					// Not consecutive, save current range and start new one
					if (start == end) {
						ranges.Add(start.ToString());
					}
					else {
						ranges.Add(String.Format("{0}-{1}", start, end));
					}
					start = sorted[i];
					end = sorted[i];
				}
			}

			// Add last range
			if (start == end) {
				ranges.Add(start.ToString());
			}
			else {
				ranges.Add(String.Format("{0}-{1}", start, end));
			}

			return String.Join(",", ranges.ToArray());
		}

		private void _paletteSelector_SelectionChanged(object sender, ObservabableListEventArgs args) {
			int[] selectedIndices = _paletteSelector.SelectedItems.Cast<int>().ToArray();
			string formatted = FormatSelectedIndices(selectedIndices);
			_tbSelectedIndices.Text = formatted;
			// Preview update removed - now requires clicking Apply button
		}

		private void _rbMode_Checked(object sender, RoutedEventArgs e) {
			if (_gbHsvMode == null || _gbColorizeMode == null || _gbGrayscaleMode == null)
				return;

			if (_rbModeHsv.IsChecked == true) {
				_gbHsvMode.Visibility = Visibility.Visible;
				_gbColorizeMode.Visibility = Visibility.Collapsed;
				_gbGrayscaleMode.Visibility = Visibility.Collapsed;
			}
			else if (_rbModeColorize.IsChecked == true) {
				_gbHsvMode.Visibility = Visibility.Collapsed;
				_gbColorizeMode.Visibility = Visibility.Visible;
				_gbGrayscaleMode.Visibility = Visibility.Collapsed;
			}
			else if (_rbModeGrayscale.IsChecked == true) {
				_gbHsvMode.Visibility = Visibility.Collapsed;
				_gbColorizeMode.Visibility = Visibility.Collapsed;
				_gbGrayscaleMode.Visibility = Visibility.Visible;
			}

			// Preview update removed when mode changes - now requires clicking Apply button
		}

		private void ApplyPreviewTransformations(Pal targetPalette, Pal sourcePalette, int[] selectedIndices, PaletteGeneratorEngine.GenerationMode mode, PaletteGeneratorEngine.GenerationParameters parameters) {
			if (targetPalette == null || sourcePalette == null || selectedIndices == null || selectedIndices.Length == 0) {
				return;
			}

			if (mode == PaletteGeneratorEngine.GenerationMode.HsvStandard) {

				// Apply HSV Standard transformations (using first variation for preview)
				double hueOffset = parameters.HueMin;
				double satOffset = parameters.SaturationMin;
				double ligOffset = parameters.LightnessMin;

				foreach (int index in selectedIndices) {
					if (index < 0 || index >= 256) continue;

					GrfColor originalColor = sourcePalette.GetColor(index);
					double originalHue = originalColor.Hue;
					double originalSat = originalColor.Hsl.S;
					double originalLig = originalColor.Lightness;

					// Apply offsets (normalized: hue is 0-1, saturation/lightness are 0-1)
					double newHue = (originalHue + hueOffset / 360.0) % 1.0;
					if (newHue < 0) newHue += 1.0;

					double newSat = GrfColor.ClampDouble(originalSat + satOffset);
					double newLig = GrfColor.ClampDouble(originalLig + ligOffset);

					GrfColor newColor = GrfColor.FromHsl(newHue, newSat, newLig, originalColor.A);
					targetPalette.SetBytes(index * 4, newColor.ToRgbaBytes());
				}
			}
			else if (mode == PaletteGeneratorEngine.GenerationMode.Grayscale) {
				// Apply Grayscale transformations (using medium tone for preview)
				foreach (int index in selectedIndices) {
					if (index < 0 || index >= 256) continue;

					GrfColor originalColor = sourcePalette.GetColor(index);
					
					// Use the engine's conversion method - need to access it through a public method
					// For now, we'll duplicate the logic here for preview
					double luminance = (0.299 * originalColor.R + 0.587 * originalColor.G + 0.114 * originalColor.B) / 255.0;
					double adjustedLuminance = luminance * parameters.GrayscaleMediumTone;
					adjustedLuminance = (adjustedLuminance - 0.5) * (1.0 + parameters.GrayscaleContrast) + 0.5;
					adjustedLuminance = adjustedLuminance + parameters.GrayscaleBrightness;
					adjustedLuminance = GrfColor.ClampDouble(adjustedLuminance);
					
					if (parameters.GrayscaleType == PaletteGeneratorEngine.GrayscaleType.BlackWhite) {
						adjustedLuminance = adjustedLuminance > 0.5 ? 1.0 : 0.0;
					}
					
					byte grayValue = (byte)(adjustedLuminance * 255);
					GrfColor grayscaleColor = new GrfColor(grayValue, grayValue, grayValue, originalColor.A);

					targetPalette.SetBytes(index * 4, grayscaleColor.ToRgbaBytes());
				}
			}
			else {
				// Colorize mode - parameters already set in UpdatePreview
				// Classify colors into light, medium, and dark based on lightness
				List<int> lightIndices = new List<int>();
				List<int> mediumIndices = new List<int>();
				List<int> darkIndices = new List<int>();

				foreach (int index in selectedIndices) {
					if (index < 0 || index >= 256) continue;

					GrfColor color = sourcePalette.GetColor(index);
					double lightness = color.Lightness;

					if (lightness > 0.66) {
						lightIndices.Add(index);
					}
					else if (lightness > 0.33) {
						mediumIndices.Add(index);
					}
					else {
						darkIndices.Add(index);
					}
				}

				// Apply colorize transformations
				ApplyColorizeToIndices(targetPalette, sourcePalette, lightIndices, parameters.ColorizeHueLight, parameters.ColorizeSaturation, parameters.ColorizeBrightness);
				ApplyColorizeToIndices(targetPalette, sourcePalette, mediumIndices, parameters.ColorizeHueMedium, parameters.ColorizeSaturation, parameters.ColorizeBrightness);
				ApplyColorizeToIndices(targetPalette, sourcePalette, darkIndices, parameters.ColorizeHueDark, parameters.ColorizeSaturation, parameters.ColorizeBrightness);
			}
		}

		private void ApplyColorizeToIndices(Pal targetPalette, Pal sourcePalette, List<int> indices, double hueOffset, double saturationOffset, double brightnessOffset) {
			foreach (int index in indices) {
				if (index < 0 || index >= 256) continue;

				GrfColor originalColor = sourcePalette.GetColor(index);
				double originalHue = originalColor.Hue;
				double originalSat = originalColor.Hsl.S;
				double originalLig = originalColor.Lightness;

				// Colorize mode: Replace the hue with the target hue (not add offset)
				// This creates vibrant, distinctly different colors from the base
				// Convert hueOffset from degrees to normalized (0-1)
				double targetHue = (hueOffset / 360.0) % 1.0;
				if (targetHue < 0) targetHue += 1.0;
				double newHue = targetHue;

				// Apply saturation and brightness offsets
				double newSat = GrfColor.ClampDouble(originalSat + saturationOffset);
				double newLig = GrfColor.ClampDouble(originalLig + brightnessOffset);

				GrfColor newColor = GrfColor.FromHsl(newHue, newSat, newLig, originalColor.A);
				targetPalette.SetBytes(index * 4, newColor.ToRgbaBytes());
			}
		}

		private BitmapSource RenderActPreview(int actionIndex, int frameIndex, Pal modifiedPalette) {
			if (_sourceAct == null || actionIndex < 0 || actionIndex >= _sourceAct.NumberOfActions) {
				return null;
			}

			ActAction action = _sourceAct[actionIndex];
			if (frameIndex < 0 || frameIndex >= action.NumberOfFrames) {
				return null;
			}

			try {
				// Create a temporary copy of the sprite images with modified palette
				List<GrfImage> originalImages = new List<GrfImage>();
				List<byte[]> originalPalettes = new List<byte[]>();

				// Backup original palettes
				for (int i = 0; i < _sourceAct.Sprite.NumberOfIndexed8Images; i++) {
					GrfImage img = _sourceAct.Sprite.Images[i];
					if (img.GrfImageType == GrfImageType.Indexed8) {
						originalImages.Add(img);
						byte[] paletteCopy = new byte[1024];
						Buffer.BlockCopy(img.Palette, 0, paletteCopy, 0, 1024);
						originalPalettes.Add(paletteCopy);

						// Apply modified palette
						Buffer.BlockCopy(modifiedPalette.BytePalette, 0, img.Palette, 0, 1024);
						img.Palette[3] = 0; // Set transparency byte
					}
				}

				// Generate frame image
				ImageSource source = Imaging.GenerateImage(_sourceAct, actionIndex, frameIndex);
				BitmapSource bitmapSource = Imaging.ForceRender(source, BitmapScalingMode.NearestNeighbor);

				// Restore original palettes
				for (int i = 0; i < originalImages.Count; i++) {
					Buffer.BlockCopy(originalPalettes[i], 0, originalImages[i].Palette, 0, 1024);
				}

				return bitmapSource;
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
				return null;
			}
		}

		private void UpdatePreview() {
			if (_isUpdatingPreview || _currentPalette == null || _sourceAct == null) {
				return;
			}

			_isUpdatingPreview = true;

			// Read all UI properties on UI thread before starting background thread
			int[] selectedIndices = _paletteSelector.SelectedItems.Cast<int>().ToArray();
			if (selectedIndices.Length == 0) {
				_imgPreview.Source = null;
				_isUpdatingPreview = false;
				return;
			}

			PaletteGeneratorEngine.GenerationMode mode;
			if (_rbModeHsv.IsChecked == true) {
				mode = PaletteGeneratorEngine.GenerationMode.HsvStandard;
			}
			else if (_rbModeColorize.IsChecked == true) {
				mode = PaletteGeneratorEngine.GenerationMode.Colorize;
			}
			else {
				mode = PaletteGeneratorEngine.GenerationMode.Grayscale;
			}

			PaletteGeneratorEngine.GenerationParameters parameters = new PaletteGeneratorEngine.GenerationParameters();

			// HueMin and HueMax are needed for both modes to calculate hue range for variations
			// Use the appropriate fields based on the current mode
			if (mode == PaletteGeneratorEngine.GenerationMode.HsvStandard) {
				parameters.HueMin = ParseDouble(_tbHueMin.Text, 0);
				parameters.HueMax = ParseDouble(_tbHueMax.Text, 360);
				parameters.HueStep = ParseDouble(_tbHueStep.Text, 10);
				parameters.SaturationMin = ParseDouble(_tbSaturationMin.Text, 0);
				parameters.SaturationMax = ParseDouble(_tbSaturationMax.Text, 0);
				parameters.SaturationStep = ParseDouble(_tbSaturationStep.Text, 0);
				parameters.LightnessMin = ParseDouble(_tbLightnessMin.Text, 0);
				parameters.LightnessMax = ParseDouble(_tbLightnessMax.Text, 0);
				parameters.LightnessStep = ParseDouble(_tbLightnessStep.Text, 0);
			}
			else if (mode == PaletteGeneratorEngine.GenerationMode.Colorize) {
				parameters.HueMin = ParseDouble(_tbColorizeHueMin.Text, 0);
				parameters.HueMax = ParseDouble(_tbColorizeHueMax.Text, 360);
				parameters.ColorizeHueLight = ParseDouble(_tbColorizeHueLight.Text, 0);
				parameters.ColorizeHueMedium = ParseDouble(_tbColorizeHueMedium.Text, 0);
				parameters.ColorizeHueDark = ParseDouble(_tbColorizeHueDark.Text, 0);
				parameters.ColorizeSaturation = ParseDouble(_tbColorizeSaturation.Text, 0);
				parameters.ColorizeBrightness = ParseDouble(_tbColorizeBrightness.Text, 0);
			}
			else if (mode == PaletteGeneratorEngine.GenerationMode.Grayscale) {
				if (_rbGrayscaleTypeBlackWhite.IsChecked == true) {
					parameters.GrayscaleType = PaletteGeneratorEngine.GrayscaleType.BlackWhite;
				}
				else if (_rbGrayscaleTypeGray.IsChecked == true) {
					parameters.GrayscaleType = PaletteGeneratorEngine.GrayscaleType.Gray;
				}
				else {
					parameters.GrayscaleType = PaletteGeneratorEngine.GrayscaleType.Both;
				}
				parameters.GrayscaleLightTone = ParseDouble(_tbGrayscaleLightTone.Text, 0.8);
				parameters.GrayscaleMediumTone = ParseDouble(_tbGrayscaleMediumTone.Text, 0.5);
				parameters.GrayscaleDarkTone = ParseDouble(_tbGrayscaleDarkTone.Text, 0.2);
				parameters.GrayscaleContrast = ParseDouble(_tbGrayscaleContrast.Text, 0);
				parameters.GrayscaleBrightness = ParseDouble(_tbGrayscaleBrightness.Text, 0);
			}

			int actionIndex = _cbPreviewAction.SelectedIndex >= 0 ? _cbPreviewAction.SelectedIndex : 0;
			int frameIndex = _cbPreviewFrame.SelectedIndex >= 0 ? _cbPreviewFrame.SelectedIndex : 0;

			GrfThread.Start(() => {
				try {
					// Create a copy of the base palette
					byte[] paletteData = new byte[1024];
					Buffer.BlockCopy(_currentPalette.BytePalette, 0, paletteData, 0, 1024);
					Pal previewPalette = new Pal(paletteData);

					// Apply transformations
					ApplyPreviewTransformations(previewPalette, _currentPalette, selectedIndices, mode, parameters);

					// Render preview must be done on UI thread (STA required)
					this.Dispatch(() => {
						try {
							BitmapSource previewImage = RenderActPreview(actionIndex, frameIndex, previewPalette);
							_imgPreview.Source = previewImage;
							_isUpdatingPreview = false;
						}
						catch (Exception err) {
							ErrorHandler.HandleException(err, ErrorLevel.Warning);
							_imgPreview.Source = null;
							_isUpdatingPreview = false;
						}
					});
				}
				catch (Exception err) {
					this.Dispatch(() => {
						ErrorHandler.HandleException(err, ErrorLevel.Warning);
						_imgPreview.Source = null;
						_isUpdatingPreview = false;
					});
				}
			});
		}

		private void _btnSelectAll_Click(object sender, RoutedEventArgs e) {
			if (_currentPalette == null) return;

			List<int> allIndices = Enumerable.Range(0, 256).ToList();
			_paletteSelector.SelectedItems.Clear();
			foreach (int index in allIndices) {
				_paletteSelector.SelectedItems.Add(index);
			}
		}

		private void _btnSelectNone_Click(object sender, RoutedEventArgs e) {
			_paletteSelector.SelectedItems.Clear();
		}

		private void _btnGenerate_Click(object sender, RoutedEventArgs e) {
			try {
				if (!ValidateInputs()) {
					return;
				}

				SaveConfiguration();

				int[] selectedIndices = _paletteSelector.SelectedItems.Cast<int>().ToArray();
				PaletteGeneratorEngine.GenerationMode mode;
				if (_rbModeHsv.IsChecked == true) {
					mode = PaletteGeneratorEngine.GenerationMode.HsvStandard;
				}
				else if (_rbModeColorize.IsChecked == true) {
					mode = PaletteGeneratorEngine.GenerationMode.Colorize;
				}
				else {
					mode = PaletteGeneratorEngine.GenerationMode.Grayscale;
				}

				PaletteGeneratorEngine.GenerationParameters parameters = new PaletteGeneratorEngine.GenerationParameters();

				// HueMin and HueMax are needed for both modes to calculate hue range for variations
				// Use the appropriate fields based on the current mode
				if (mode == PaletteGeneratorEngine.GenerationMode.HsvStandard) {
					parameters.HueMin = ParseDouble(_tbHueMin.Text, 0);
					parameters.HueMax = ParseDouble(_tbHueMax.Text, 360);
					parameters.HueStep = ParseDouble(_tbHueStep.Text, 10);
					parameters.SaturationMin = ParseDouble(_tbSaturationMin.Text, 0);
					parameters.SaturationMax = ParseDouble(_tbSaturationMax.Text, 0);
					parameters.SaturationStep = ParseDouble(_tbSaturationStep.Text, 0);
					parameters.LightnessMin = ParseDouble(_tbLightnessMin.Text, 0);
					parameters.LightnessMax = ParseDouble(_tbLightnessMax.Text, 0);
					parameters.LightnessStep = ParseDouble(_tbLightnessStep.Text, 0);
				}
				else if (mode == PaletteGeneratorEngine.GenerationMode.Colorize) {
					parameters.HueMin = ParseDouble(_tbColorizeHueMin.Text, 0);
					parameters.HueMax = ParseDouble(_tbColorizeHueMax.Text, 360);
					parameters.ColorizeHueLight = ParseDouble(_tbColorizeHueLight.Text, 0);
					parameters.ColorizeHueMedium = ParseDouble(_tbColorizeHueMedium.Text, 0);
					parameters.ColorizeHueDark = ParseDouble(_tbColorizeHueDark.Text, 0);
					parameters.ColorizeSaturation = ParseDouble(_tbColorizeSaturation.Text, 0);
					parameters.ColorizeBrightness = ParseDouble(_tbColorizeBrightness.Text, 0);
				}
				else if (mode == PaletteGeneratorEngine.GenerationMode.Grayscale) {
					if (_rbGrayscaleTypeBlackWhite.IsChecked == true) {
						parameters.GrayscaleType = PaletteGeneratorEngine.GrayscaleType.BlackWhite;
					}
					else if (_rbGrayscaleTypeGray.IsChecked == true) {
						parameters.GrayscaleType = PaletteGeneratorEngine.GrayscaleType.Gray;
					}
					else {
						parameters.GrayscaleType = PaletteGeneratorEngine.GrayscaleType.Both;
					}
					parameters.GrayscaleLightTone = ParseDouble(_tbGrayscaleLightTone.Text, 0.8);
					parameters.GrayscaleMediumTone = ParseDouble(_tbGrayscaleMediumTone.Text, 0.5);
					parameters.GrayscaleDarkTone = ParseDouble(_tbGrayscaleDarkTone.Text, 0.2);
					parameters.GrayscaleContrast = ParseDouble(_tbGrayscaleContrast.Text, 0);
					parameters.GrayscaleBrightness = ParseDouble(_tbGrayscaleBrightness.Text, 0);
				}

				string outputFolder = _pbOutputFolder.Text;
				string fileNamePrefix = _tbFileNamePrefix.Text;
				int numberOfVariations = ParseInt(_tbNumberOfVariations.Text, 10);

				// Get file naming parameters
				string className = _cbClass.SelectedValue != null ? _cbClass.SelectedValue.ToString() : null;
				string gender = _rbGenderFemale.IsChecked == true ? "¿©" : "³²";
				bool isCostume = _rbSpriteTypeCostume.IsChecked == true;
				int costumeNumber = isCostume ? ParseInt(_tbCostumeNumber.Text, 1) : 0;

				_btnGenerate.IsEnabled = false;
				_tbStatus.Text = "Generating palettes...";

				GrfThread.Start(() => {
					try {
						List<string> generatedFiles = _engine.GeneratePalettes(
							_currentPalette,
							selectedIndices,
							mode,
							parameters,
							outputFolder,
							fileNamePrefix,
							numberOfVariations,
							className,
							gender,
							isCostume,
							costumeNumber
						);

						this.Dispatch(() => {
							_tbStatus.Text = String.Format("Generated {0} palette file(s) successfully", generatedFiles.Count);
							_btnGenerate.IsEnabled = true;

							if (generatedFiles.Count > 0) {
								string message = String.Format("Successfully generated {0} palette file(s):\n\n", generatedFiles.Count);
								string[] fileNames = generatedFiles.Take(10).Select(f => Path.GetFileName(f)).ToArray();
								message += String.Join("\n", fileNames);
								if (generatedFiles.Count > 10) {
									message += String.Format("\n... and {0} more", generatedFiles.Count - 10);
								}
								MessageBox.Show(this, message, "Generation Complete", MessageBoxButton.OK, MessageBoxImage.Information);
							}
						});
					}
					catch (Exception err) {
						this.Dispatch(() => {
							ErrorHandler.HandleException(err);
							_tbStatus.Text = "Error during generation";
							_btnGenerate.IsEnabled = true;
						});
					}
				});
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				_tbStatus.Text = "Error: " + err.Message;
			}
		}

		private void _btnCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private bool ValidateInputs() {
			if (_currentPalette == null) {
				MessageBox.Show(this, "No palette available. Please ensure the ACT has a valid palette.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
				return false;
			}

			if (_paletteSelector.SelectedItems.Count == 0) {
				MessageBox.Show(this, "Please select at least one palette index to modify.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
				return false;
			}

			if (String.IsNullOrEmpty(_pbOutputFolder.Text)) {
				MessageBox.Show(this, "Please select an output folder.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
				return false;
			}

			if (_cbClass.SelectedValue == null || String.IsNullOrEmpty(_cbClass.SelectedValue.ToString())) {
				MessageBox.Show(this, "Please select a character class.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
				return false;
			}

			if (_rbSpriteTypeCostume.IsChecked == true) {
				int costumeNumber = ParseInt(_tbCostumeNumber.Text, 0);
				if (costumeNumber < 1) {
					MessageBox.Show(this, "Costume number must be greater than 0.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
					return false;
				}
			}

			int numberOfVariations = ParseInt(_tbNumberOfVariations.Text, 0);
			if (numberOfVariations <= 0) {
				MessageBox.Show(this, "Number of variations must be greater than 0.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
				return false;
			}

			return true;
		}

		private void LoadConfiguration() {
			try {
				_pbOutputFolder.Text = PaletteGeneratorConfig.LastOutputFolder;
				_tbFileNamePrefix.Text = PaletteGeneratorConfig.LastFileNamePrefix;
				_tbNumberOfVariations.Text = PaletteGeneratorConfig.LastNumberOfVariations.ToString(CultureInfo.InvariantCulture);

				int mode = PaletteGeneratorConfig.LastMode;
				if (mode == 0) {
					_rbModeHsv.IsChecked = true;
				}
				else if (mode == 1) {
					_rbModeColorize.IsChecked = true;
				}
				else {
					_rbModeGrayscale.IsChecked = true;
				}

				// Load HSV parameters
				_isSyncingHueRange = true; // Prevent sync during initial load
				_tbHueMin.Text = PaletteGeneratorConfig.LastHueMin.ToString(CultureInfo.InvariantCulture);
				_tbHueMax.Text = PaletteGeneratorConfig.LastHueMax.ToString(CultureInfo.InvariantCulture);
				_tbHueStep.Text = PaletteGeneratorConfig.LastHueStep.ToString(CultureInfo.InvariantCulture);
				// Calculate range from min/max
				double hueRange = PaletteGeneratorConfig.LastHueMax - PaletteGeneratorConfig.LastHueMin;
				_tbHueRange.Text = hueRange.ToString(CultureInfo.InvariantCulture);
				_isSyncingHueRange = false;
				_tbSaturationMin.Text = PaletteGeneratorConfig.LastSaturationMin.ToString(CultureInfo.InvariantCulture);
				_tbSaturationMax.Text = PaletteGeneratorConfig.LastSaturationMax.ToString(CultureInfo.InvariantCulture);
				_tbSaturationStep.Text = PaletteGeneratorConfig.LastSaturationStep.ToString(CultureInfo.InvariantCulture);
				_tbLightnessMin.Text = PaletteGeneratorConfig.LastLightnessMin.ToString(CultureInfo.InvariantCulture);
				_tbLightnessMax.Text = PaletteGeneratorConfig.LastLightnessMax.ToString(CultureInfo.InvariantCulture);
				_tbLightnessStep.Text = PaletteGeneratorConfig.LastLightnessStep.ToString(CultureInfo.InvariantCulture);

				// Load Colorize parameters
				// Use same hue min/max/range as HSV mode (both modes share these for variation range)
				_isSyncingHueRange = true; // Prevent sync during initial load
				_tbColorizeHueMin.Text = PaletteGeneratorConfig.LastHueMin.ToString(CultureInfo.InvariantCulture);
				_tbColorizeHueMax.Text = PaletteGeneratorConfig.LastHueMax.ToString(CultureInfo.InvariantCulture);
				double colorizeHueRange = PaletteGeneratorConfig.LastHueMax - PaletteGeneratorConfig.LastHueMin;
				_tbColorizeHueRange.Text = colorizeHueRange.ToString(CultureInfo.InvariantCulture);
				_isSyncingHueRange = false;
				_tbColorizeHueLight.Text = PaletteGeneratorConfig.LastColorizeHueLight.ToString(CultureInfo.InvariantCulture);
				_tbColorizeHueMedium.Text = PaletteGeneratorConfig.LastColorizeHueMedium.ToString(CultureInfo.InvariantCulture);
				_tbColorizeHueDark.Text = PaletteGeneratorConfig.LastColorizeHueDark.ToString(CultureInfo.InvariantCulture);
				_tbColorizeSaturation.Text = PaletteGeneratorConfig.LastColorizeSaturation.ToString(CultureInfo.InvariantCulture);
				_tbColorizeBrightness.Text = PaletteGeneratorConfig.LastColorizeBrightness.ToString(CultureInfo.InvariantCulture);

				// Load Grayscale parameters
				int grayscaleType = PaletteGeneratorConfig.LastGrayscaleType;
				if (grayscaleType == 0) {
					_rbGrayscaleTypeBlackWhite.IsChecked = true;
				}
				else if (grayscaleType == 1) {
					_rbGrayscaleTypeGray.IsChecked = true;
				}
				else {
					_rbGrayscaleTypeBoth.IsChecked = true;
				}
				_tbGrayscaleLightTone.Text = PaletteGeneratorConfig.LastGrayscaleLightTone.ToString(CultureInfo.InvariantCulture);
				_tbGrayscaleMediumTone.Text = PaletteGeneratorConfig.LastGrayscaleMediumTone.ToString(CultureInfo.InvariantCulture);
				_tbGrayscaleDarkTone.Text = PaletteGeneratorConfig.LastGrayscaleDarkTone.ToString(CultureInfo.InvariantCulture);
				_tbGrayscaleContrast.Text = PaletteGeneratorConfig.LastGrayscaleContrast.ToString(CultureInfo.InvariantCulture);
				_tbGrayscaleBrightness.Text = PaletteGeneratorConfig.LastGrayscaleBrightness.ToString(CultureInfo.InvariantCulture);

				// Load file naming configuration
				string lastClass = PaletteGeneratorConfig.LastSelectedClass;
				if (!String.IsNullOrEmpty(lastClass)) {
					_cbClass.SelectedValue = lastClass;
				}

				string lastGender = PaletteGeneratorConfig.LastSelectedGender;
				if (lastGender == "¿©") {
					_rbGenderFemale.IsChecked = true;
				}
				else {
					_rbGenderMale.IsChecked = true;
				}

				int spriteType = PaletteGeneratorConfig.LastSelectedSpriteType;
				if (spriteType == 1) {
					_rbSpriteTypeCostume.IsChecked = true;
				}
				else {
					_rbSpriteTypeDefault.IsChecked = true;
				}

				_tbCostumeNumber.Text = PaletteGeneratorConfig.LastCostumeNumber.ToString(CultureInfo.InvariantCulture);
				UpdateCostumeNumberVisibility();

				// Load selected indices
				int[] indices = PaletteGeneratorConfig.GetSelectedIndices();
				if (indices.Length > 0 && _currentPalette != null) {
					_paletteSelector.SelectedItems.Clear();
					foreach (int index in indices) {
						if (index >= 0 && index < 256) {
							_paletteSelector.SelectedItems.Add(index);
						}
					}
					// Update selected indices display
					string formatted = FormatSelectedIndices(indices);
					_tbSelectedIndices.Text = formatted;
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void SaveConfiguration() {
			try {
				PaletteGeneratorConfig.LastOutputFolder = _pbOutputFolder.Text;
				PaletteGeneratorConfig.LastFileNamePrefix = _tbFileNamePrefix.Text;
				PaletteGeneratorConfig.LastNumberOfVariations = ParseInt(_tbNumberOfVariations.Text, 10);

				if (_rbModeHsv.IsChecked == true) {
					PaletteGeneratorConfig.LastMode = 0;
				}
				else if (_rbModeColorize.IsChecked == true) {
					PaletteGeneratorConfig.LastMode = 1;
				}
				else {
					PaletteGeneratorConfig.LastMode = 2;
				}

				// Save HSV parameters
				PaletteGeneratorConfig.LastHueMin = ParseDouble(_tbHueMin.Text, 0);
				PaletteGeneratorConfig.LastHueMax = ParseDouble(_tbHueMax.Text, 360);
				PaletteGeneratorConfig.LastHueStep = ParseDouble(_tbHueStep.Text, 10);
				PaletteGeneratorConfig.LastHueRange = ParseDouble(_tbHueRange.Text, 360);
				PaletteGeneratorConfig.LastSaturationMin = ParseDouble(_tbSaturationMin.Text, 0);
				PaletteGeneratorConfig.LastSaturationMax = ParseDouble(_tbSaturationMax.Text, 0);
				PaletteGeneratorConfig.LastSaturationStep = ParseDouble(_tbSaturationStep.Text, 0);
				PaletteGeneratorConfig.LastLightnessMin = ParseDouble(_tbLightnessMin.Text, 0);
				PaletteGeneratorConfig.LastLightnessMax = ParseDouble(_tbLightnessMax.Text, 0);
				PaletteGeneratorConfig.LastLightnessStep = ParseDouble(_tbLightnessStep.Text, 0);

				// Save Colorize parameters
				// Save hue min/max/range (shared with HSV mode for variation range)
				// If Colorize mode is active, use Colorize fields; otherwise keep HSV values
				if (_rbModeColorize.IsChecked == true) {
					PaletteGeneratorConfig.LastHueMin = ParseDouble(_tbColorizeHueMin.Text, 0);
					PaletteGeneratorConfig.LastHueMax = ParseDouble(_tbColorizeHueMax.Text, 360);
					PaletteGeneratorConfig.LastHueRange = ParseDouble(_tbColorizeHueRange.Text, 360);
				}
				PaletteGeneratorConfig.LastColorizeHueLight = ParseDouble(_tbColorizeHueLight.Text, 0);
				PaletteGeneratorConfig.LastColorizeHueMedium = ParseDouble(_tbColorizeHueMedium.Text, 0);
				PaletteGeneratorConfig.LastColorizeHueDark = ParseDouble(_tbColorizeHueDark.Text, 0);
				PaletteGeneratorConfig.LastColorizeSaturation = ParseDouble(_tbColorizeSaturation.Text, 0);
				PaletteGeneratorConfig.LastColorizeBrightness = ParseDouble(_tbColorizeBrightness.Text, 0);

				// Save Grayscale parameters
				int grayscaleType = 0;
				if (_rbGrayscaleTypeBlackWhite.IsChecked == true) {
					grayscaleType = 0;
				}
				else if (_rbGrayscaleTypeGray.IsChecked == true) {
					grayscaleType = 1;
				}
				else {
					grayscaleType = 2;
				}
				PaletteGeneratorConfig.LastGrayscaleType = grayscaleType;
				PaletteGeneratorConfig.LastGrayscaleLightTone = ParseDouble(_tbGrayscaleLightTone.Text, 0.8);
				PaletteGeneratorConfig.LastGrayscaleMediumTone = ParseDouble(_tbGrayscaleMediumTone.Text, 0.5);
				PaletteGeneratorConfig.LastGrayscaleDarkTone = ParseDouble(_tbGrayscaleDarkTone.Text, 0.2);
				PaletteGeneratorConfig.LastGrayscaleContrast = ParseDouble(_tbGrayscaleContrast.Text, 0);
				PaletteGeneratorConfig.LastGrayscaleBrightness = ParseDouble(_tbGrayscaleBrightness.Text, 0);

				// Save file naming configuration
				if (_cbClass.SelectedValue != null) {
					PaletteGeneratorConfig.LastSelectedClass = _cbClass.SelectedValue.ToString();
				}

				string gender = _rbGenderFemale.IsChecked == true ? "¿©" : "³²";
				PaletteGeneratorConfig.LastSelectedGender = gender;

				int spriteType = _rbSpriteTypeCostume.IsChecked == true ? 1 : 0;
				PaletteGeneratorConfig.LastSelectedSpriteType = spriteType;

				PaletteGeneratorConfig.LastCostumeNumber = ParseInt(_tbCostumeNumber.Text, 1);

				// Save selected indices
				int[] indices = _paletteSelector.SelectedItems.Cast<int>().ToArray();
				PaletteGeneratorConfig.SetSelectedIndices(indices);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private double ParseDouble(string text, double defaultValue) {
			if (String.IsNullOrEmpty(text))
				return defaultValue;

			try {
				return Double.Parse(text.Replace(",", "."), CultureInfo.InvariantCulture);
			}
			catch {
				return defaultValue;
			}
		}

		private int ParseInt(string text, int defaultValue) {
			if (String.IsNullOrEmpty(text))
				return defaultValue;

			try {
				return Int32.Parse(text);
			}
			catch {
				return defaultValue;
			}
		}
	}
}
