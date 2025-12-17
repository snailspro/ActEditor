using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
		private List<ColorGroup> _colorGroups = new List<ColorGroup>();
		private ColorGroup _selectedGroup = null;
		private bool _isUpdatingGroup = false; // Flag para evitar loops ao atualizar grupo
		private int _skinColorType = 0; // 0 = Pele Clara, 1 = Pele Negra
		private bool _isPreviewPlaying = false;
		private int _currentPreviewFrameIndex = 0;
		private List<int> _currentVariationIndices = null; // Store current variation indices for preview

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
				_groupPaletteSelector.SetPalette(_currentPalette);
				_groupPaletteSelector.SelectionChanged += _groupPaletteSelector_SelectionChanged;
				
				// Update ACT info label if available
				if (!String.IsNullOrEmpty(_sourceAct.LoadedPath)) {
					string actName = Path.GetFileName(_sourceAct.LoadedPath);
					_tbActInfo.Text = String.Format("Origem: {0}", actName);
				}
				else {
					_tbActInfo.Text = "Origem: ACT Atual";
				}
				
				_tbStatus.Text = "Paleta carregada do ACT com sucesso";
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				_tbStatus.Text = "Error loading palette from ACT";
			}

			// Populate class ComboBox
			PopulateClassComboBox();
			
			// Initialize groups
			InitializeGroups();
			
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
			
			// Initialize play button
			_currentPreviewFrameIndex = 0;
			_isPreviewPlaying = false;
			_updatePlayButton();
			
			// Stop animation when window closes
			Closed += (s, evt) => {
				if (_isPreviewPlaying) {
					_btnPlayPreview.IsPressed = false;
					_isPreviewPlaying = false;
				}
			};
			
			// Connect text change events for debounced preview updates
			ConnectParameterEvents();
			
			// Connect hue range synchronization events
			ConnectHueRangeEvents();
			
			// Connect parameter change events to save group
			ConnectGroupParameterEvents();
			
			// Load skin color type (after InitializeComponent)
			_skinColorType = PaletteGeneratorConfig.LastSkinColorType;
			if (_cbSkinColor != null && _skinColorType >= 0 && _skinColorType < _cbSkinColor.Items.Count) {
				_cbSkinColor.SelectedIndex = _skinColorType;
			}

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

			// Get main animations (direction 0 of each animation)
			// Each animation has 8 directions (0-7), so main actions are: 0, 8, 16, 24, etc.
			int numberOfAnimations = (int)Math.Ceiling(_sourceAct.NumberOfActions / 8f);
			
			// Get animation names if available
			try {
				var animationStrings = _sourceAct.GetAnimationStrings();
				if (animationStrings != null && animationStrings.Count == numberOfAnimations) {
					// Use animation names
					for (int i = 0; i < numberOfAnimations; i++) {
						int actionIndex = i * 8;
						if (actionIndex < _sourceAct.NumberOfActions) {
							_cbPreviewAction.Items.Add(animationStrings[i]);
							// Store action index as Tag for later use
							// We'll use SelectedIndex * 8 to get the actual action index
						}
					}
				}
				else {
					// Fallback: use action indices
					for (int i = 0; i < numberOfAnimations; i++) {
						int actionIndex = i * 8;
						if (actionIndex < _sourceAct.NumberOfActions) {
							_cbPreviewAction.Items.Add(String.Format("Ação {0}", actionIndex));
						}
					}
				}
			}
			catch {
				// Fallback: use action indices
				for (int i = 0; i < numberOfAnimations; i++) {
					int actionIndex = i * 8;
					if (actionIndex < _sourceAct.NumberOfActions) {
						_cbPreviewAction.Items.Add(String.Format("Ação {0}", actionIndex));
					}
				}
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

		private void ConnectGroupParameterEvents() {
			// Connect all parameter text boxes to save group when changed
			_tbHueMin.TextChanged += _parameter_TextChanged;
			_tbHueMax.TextChanged += _parameter_TextChanged;
			_tbHueStep.TextChanged += _parameter_TextChanged;
			_tbHueRange.TextChanged += _parameter_TextChanged;
			_tbSaturationMin.TextChanged += _parameter_TextChanged;
			_tbSaturationMax.TextChanged += _parameter_TextChanged;
			_tbSaturationStep.TextChanged += _parameter_TextChanged;
			_tbLightnessMin.TextChanged += _parameter_TextChanged;
			_tbLightnessMax.TextChanged += _parameter_TextChanged;
			_tbLightnessStep.TextChanged += _parameter_TextChanged;
			
			_tbColorizeHueMin.TextChanged += _parameter_TextChanged;
			_tbColorizeHueMax.TextChanged += _parameter_TextChanged;
			_tbColorizeHueRange.TextChanged += _parameter_TextChanged;
			_tbColorizeHueLight.TextChanged += _parameter_TextChanged;
			_tbColorizeHueMedium.TextChanged += _parameter_TextChanged;
			_tbColorizeHueDark.TextChanged += _parameter_TextChanged;
			_tbColorizeSaturation.TextChanged += _parameter_TextChanged;
			_tbColorizeBrightness.TextChanged += _parameter_TextChanged;
			
			_tbGrayscaleLightTone.TextChanged += _parameter_TextChanged;
			_tbGrayscaleMediumTone.TextChanged += _parameter_TextChanged;
			_tbGrayscaleDarkTone.TextChanged += _parameter_TextChanged;
			_tbGrayscaleContrast.TextChanged += _parameter_TextChanged;
			_tbGrayscaleBrightness.TextChanged += _parameter_TextChanged;
			
			// Connect PreviewKeyDown handlers for arrow key increment/decrement
			_tbHueMin.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbHueMax.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbHueStep.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbHueRange.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbSaturationMin.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbSaturationMax.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbSaturationStep.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbLightnessMin.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbLightnessMax.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbLightnessStep.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			
			_tbColorizeHueMin.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbColorizeHueMax.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbColorizeHueRange.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbColorizeHueLight.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbColorizeHueMedium.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbColorizeHueDark.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbColorizeSaturation.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbColorizeBrightness.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			
			_tbGrayscaleLightTone.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbGrayscaleMediumTone.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbGrayscaleDarkTone.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbGrayscaleContrast.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			_tbGrayscaleBrightness.PreviewKeyDown += _numericTextBox_PreviewKeyDown;
			
			_rbGrayscaleTypeBlackWhite.Checked += _parameter_Checked;
			_rbGrayscaleTypeGray.Checked += _parameter_Checked;
			_rbGrayscaleTypeBoth.Checked += _parameter_Checked;
		}

		private void _parameter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
			if (_selectedGroup != null && !_isUpdatingGroup) {
				SaveGroupFromUI(_selectedGroup);
			}
		}

		private void _parameter_Checked(object sender, RoutedEventArgs e) {
			if (_selectedGroup != null && !_isUpdatingGroup) {
				SaveGroupFromUI(_selectedGroup);
			}
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
				// Stop animation if playing
				if (_isPreviewPlaying) {
					_btnPlayPreview.IsPressed = false;
					_isPreviewPlaying = false;
					_cbPreviewFrame.IsEnabled = true;
					_updatePlayButton();
				}

				_currentPreviewFrameIndex = 0;
				// Convert animation index to action index (main action = direction 0)
				int actionIndex = _cbPreviewAction.SelectedIndex * 8;
				PopulatePreviewFrames(actionIndex);
				UpdatePreview();
			}
		}

		private void _cbPreviewFrame_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_cbPreviewFrame.SelectedIndex >= 0) {
				_currentPreviewFrameIndex = _cbPreviewFrame.SelectedIndex;
				UpdatePreview();
			}
		}

		private void _btnPlayPreview_Click(object sender, RoutedEventArgs e) {
			_btnPlayPreview.IsPressed = !_btnPlayPreview.IsPressed;
			_isPreviewPlaying = _btnPlayPreview.IsPressed;
			_cbPreviewFrame.IsEnabled = !_isPreviewPlaying;
			_updatePlayButton();

			if (_isPreviewPlaying) {
				GrfThread.Start(_playPreviewAnimation);
			}
		}

		private void _playPreviewAnimation() {
			if (_sourceAct == null) {
				_btnPlayPreview_Click(null, null);
				return;
			}

			int animationIndex = this.Dispatch(() => _cbPreviewAction.SelectedIndex);
			// Convert animation index to action index (main action = direction 0)
			int actionIndex = animationIndex >= 0 ? (animationIndex * 8) : 0;
			if (actionIndex < 0 || actionIndex >= _sourceAct.NumberOfActions) {
				this.Dispatch(() => _btnPlayPreview_Click(null, null));
				return;
			}

			ActAction action = _sourceAct[actionIndex];
			if (action.NumberOfFrames <= 1) {
				this.Dispatch(() => _btnPlayPreview_Click(null, null));
				return;
			}

			if (action.AnimationSpeed < 0.8f) {
				this.Dispatch(() => _btnPlayPreview_Click(null, null));
				ErrorHandler.HandleException("A velocidade da animação é muito rápida e pode causar problemas. A animação não será exibida.", ErrorLevel.NotSpecified);
				return;
			}

			int frameInterval = ActEditorConfiguration.UseAccurateFrameInterval ? 24 : 25;
			int interval = (int)(action.AnimationSpeed * frameInterval);

			try {
				while (this.Dispatch(() => _isPreviewPlaying)) {
					// Get current animation index (may have changed)
					animationIndex = this.Dispatch(() => _cbPreviewAction.SelectedIndex);
					// Convert animation index to action index (main action = direction 0)
					actionIndex = animationIndex >= 0 ? (animationIndex * 8) : 0;
					if (actionIndex < 0 || actionIndex >= _sourceAct.NumberOfActions) {
						this.Dispatch(() => _btnPlayPreview_Click(null, null));
						return;
					}

					action = _sourceAct[actionIndex];
					if (action.AnimationSpeed < 0.8f) {
						this.Dispatch(() => _btnPlayPreview_Click(null, null));
						ErrorHandler.HandleException("A velocidade da animação é muito rápida e pode causar problemas. A animação não será exibida.", ErrorLevel.NotSpecified);
						return;
					}

					interval = (int)(action.AnimationSpeed * frameInterval);

					// Increment frame index
					_currentPreviewFrameIndex++;
					if (_currentPreviewFrameIndex >= action.NumberOfFrames) {
						_currentPreviewFrameIndex = 0;
					}

					// Update UI and preview
					this.Dispatch(() => {
						if (_cbPreviewFrame.Items.Count > _currentPreviewFrameIndex) {
							_cbPreviewFrame.SelectedIndex = _currentPreviewFrameIndex;
						}
					});

					if (!this.Dispatch(() => _isPreviewPlaying))
						return;

					Thread.Sleep(interval);
				}
			}
			catch {
				this.Dispatch(() => _btnPlayPreview_Click(null, null));
			}
		}

		private void _updatePlayButton() {
			if (_btnPlayPreview.IsPressed) {
				_btnPlayPreview.ImagePath = "stop2.png";
				_btnPlayPreview.ImageIcon.Width = 16;
				_btnPlayPreview.ImageIcon.Stretch = Stretch.Fill;
			}
			else {
				_btnPlayPreview.ImagePath = "play.png";
				_btnPlayPreview.ImageIcon.Width = 16;
				_btnPlayPreview.ImageIcon.Stretch = Stretch.Fill;
			}
		}

		private void _btnApplyPreview_Click(object sender, RoutedEventArgs e) {
			// Reset to default variations when Apply is clicked
			_currentVariationIndices = null;
			UpdatePreview();
		}

		private void _btnRandomPreview_Click(object sender, RoutedEventArgs e) {
			UpdateRandomPreview();
		}

		private void _cbSkinColor_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_cbSkinColor != null && _cbSkinColor.SelectedIndex >= 0) {
				_skinColorType = _cbSkinColor.SelectedIndex;
				PaletteGeneratorConfig.LastSkinColorType = _skinColorType;
				// Update preview when skin color changes
				if (_currentPalette != null && _sourceAct != null) {
					UpdatePreview();
				}
			}
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

					// Apply saturation and lightness with smoother scaling (divide by 10 for finer control)
					// Using multiplication: new = original * (1.0 + offset/10.0) for smoother control
					double newSat = GrfColor.ClampDouble(originalSat * (1.0 + satOffset / 10.0));
					double newLig = GrfColor.ClampDouble(originalLig * (1.0 + ligOffset / 10.0));

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

				// Apply saturation and brightness with smoother scaling (divide by 10 for finer control)
				// Using multiplication: new = original * (1.0 + offset/10.0) for smoother control
				double newSat = GrfColor.ClampDouble(originalSat * (1.0 + saturationOffset / 10.0));
				double newLig = GrfColor.ClampDouble(originalLig * (1.0 + brightnessOffset / 10.0));

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
			// Use stored variation indices if available, otherwise use null (default variations)
			UpdatePreviewWithVariations(_currentVariationIndices);
		}

		private void UpdateRandomPreview() {
			// Generate random variation indices for each group
			Random random = new Random();
			List<int> randomVariations = new List<int>();
			
			List<ColorGroup> validGroups = _colorGroups.Where(g => g.IsValid()).ToList();
			foreach (ColorGroup group in validGroups) {
				int randomVariation = random.Next(0, group.NumberOfVariations);
				randomVariations.Add(randomVariation);
			}
			
			// Store the random variations so they persist during animation
			_currentVariationIndices = randomVariations;
			UpdatePreviewWithVariations(randomVariations);
		}

		private void UpdatePreviewWithVariations(List<int> variationIndices) {
			if (_isUpdatingPreview || _currentPalette == null || _sourceAct == null) {
				return;
			}

			_isUpdatingPreview = true;

			// Save current group if selected
			if (_selectedGroup != null) {
				SaveGroupFromUI(_selectedGroup);
			}

			// Get valid groups
			List<ColorGroup> validGroups = _colorGroups.Where(g => g.IsValid()).ToList();

			// Convert animation index to action index (main action = direction 0)
			int actionIndex = _cbPreviewAction.SelectedIndex >= 0 ? (_cbPreviewAction.SelectedIndex * 8) : 0;
			int frameIndex = _isPreviewPlaying ? _currentPreviewFrameIndex : (_cbPreviewFrame.SelectedIndex >= 0 ? _cbPreviewFrame.SelectedIndex : 0);

			GrfThread.Start(() => {
				try {
					// Create a copy of the base palette
					byte[] paletteData = new byte[1024];
					Buffer.BlockCopy(_currentPalette.BytePalette, 0, paletteData, 0, 1024);
					Pal previewPalette = new Pal(paletteData);

					// Apply transformations from all groups first (if any)
					if (validGroups.Count > 0) {
						int groupIndex = 0;
						foreach (ColorGroup group in validGroups) {
							int variationIndex = (variationIndices != null && groupIndex < variationIndices.Count) 
								? variationIndices[groupIndex] 
								: 0;
							ApplyGroupTransformationPreview(previewPalette, _currentPalette, group, variationIndex);
							groupIndex++;
						}
					}

					// Apply skin color AFTER group transformations to ensure it's not overwritten
					// (applies even if no groups exist, so skin color change is always visible)
					ApplySkinColor(previewPalette, _skinColorType);

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

		private void ApplySkinColor(Pal palette, int skinColorType) {
			// Skin color indices: 32-39 and 128-135
			int[] skinIndices = { 32, 33, 34, 35, 36, 37, 38, 39, 128, 129, 130, 131, 132, 133, 134, 135 };
			
			if (skinColorType == 0) {
				// Default skin colors (applied to both sets of indices) - using hex strings to ensure correct RGB order
				GrfColor[] defaultColors = new GrfColor[] {
					new GrfColor("#FFE99F91"), // #FFE99F91
					new GrfColor("#FFFFE1CF"), // #FFFFE1CF
					new GrfColor("#FFFFC6B2"), // #FFFFC6B2
					new GrfColor("#FFF6AE9F"), // #FFF6AE9F
					new GrfColor("#FFDC9084"), // #FFDC9084
					new GrfColor("#FFBD736B"), // #FFBD736B
					new GrfColor("#FF9E5652"), // #FF9E5652
					new GrfColor("#FF823F3B")  // #FF823F3B
				};
				
				// Apply colors to indices 32-39 (using SetColor which handles ARGB format correctly)
				for (int i = 0; i < 8; i++) {
					palette.SetColor(skinIndices[i], defaultColors[i]);
				}
				// Apply same colors to indices 128-135
				for (int i = 0; i < 8; i++) {
					palette.SetColor(skinIndices[i + 8], defaultColors[i]);
				}
			}
			else if (skinColorType == 1) {
				// Cores de pele negra (aplicadas a ambos os conjuntos de índices) - usando strings hex para garantir ordem RGB correta
				GrfColor[] blackColors = new GrfColor[] {
					new GrfColor("#FFDEB3A2"), // #FFDEB3A2
					new GrfColor("#FFC39B8F"), // #FFC39B8F
					new GrfColor("#FFAF867F"), // #FFAF867F
					new GrfColor("#FF99726F"), // #FF99726F
					new GrfColor("#FF835E5F"), // #FF835E5F
					new GrfColor("#FF6D494F"), // #FF6D494F
					new GrfColor("#FF543640"), // #FF543640
					new GrfColor("#FF3B2331")  // #FF3B2331
				};
				
				// Apply colors to indices 32-39 (using SetColor which handles ARGB format correctly)
				for (int i = 0; i < 8; i++) {
					palette.SetColor(skinIndices[i], blackColors[i]);
				}
				// Apply same colors to indices 128-135
				for (int i = 0; i < 8; i++) {
					palette.SetColor(skinIndices[i + 8], blackColors[i]);
				}
			}
		}

		private void ApplyGroupTransformationPreview(Pal targetPalette, Pal sourcePalette, ColorGroup group, int variationIndex) {
			if (group == null || !group.IsValid() || group.Indices == null || group.Indices.Count == 0) {
				return;
			}

			// Exclude skin color indices (32-39 and 128-135) from group transformations
			int[] skinIndices = { 32, 33, 34, 35, 36, 37, 38, 39, 128, 129, 130, 131, 132, 133, 134, 135 };
			HashSet<int> skinIndicesSet = new HashSet<int>(skinIndices);
			int[] indices = group.Indices.Where(idx => !skinIndicesSet.Contains(idx)).ToArray();

			if (group.Mode == PaletteGeneratorEngine.GenerationMode.HsvStandard) {
				ApplyHsvStandardToIndicesPreview(targetPalette, sourcePalette, indices, group.Parameters, variationIndex, group.NumberOfVariations);
			}
			else if (group.Mode == PaletteGeneratorEngine.GenerationMode.Colorize) {
				ApplyColorizeToIndicesGroupPreview(targetPalette, sourcePalette, indices, group.Parameters, variationIndex, group.NumberOfVariations);
			}
			else if (group.Mode == PaletteGeneratorEngine.GenerationMode.Grayscale) {
				ApplyGrayscaleToIndicesPreview(targetPalette, sourcePalette, indices, group.Parameters, variationIndex, group.NumberOfVariations);
			}
		}

		private void ApplyHsvStandardToIndicesPreview(Pal targetPalette, Pal sourcePalette, int[] indices, PaletteGeneratorEngine.GenerationParameters parameters, int variationIndex, int totalVariations) {
			if (totalVariations <= 0) return;

			double hueRange = parameters.HueMax - parameters.HueMin;
			double satOffset = parameters.SaturationMin;
			double ligOffset = parameters.LightnessMin;

			int numSelectedIndices = indices.Length;
			double huePortionPerIndex = (numSelectedIndices > 0) ? hueRange / numSelectedIndices : 0;
			double variationShift = (totalVariations > 1) ? (hueRange / totalVariations) * variationIndex : 0;

			int indexPosition = 0;
			foreach (int index in indices) {
				if (index < 0 || index >= 256) continue;

				GrfColor originalColor = sourcePalette.GetColor(index);
				double originalHue = originalColor.Hue;
				double originalSat = originalColor.Hsl.S;
				double originalLig = originalColor.Lightness;

				double baseHueOffsetForIndex = huePortionPerIndex * indexPosition;
				double combinedOffset = baseHueOffsetForIndex + variationShift;

				while (combinedOffset >= hueRange) combinedOffset -= hueRange;
				while (combinedOffset < 0) combinedOffset += hueRange;

				double finalHueOffsetDegrees = combinedOffset + parameters.HueMin;
				double hueOffsetNormalized = finalHueOffsetDegrees / 360.0;
				double newHue = (originalHue + hueOffsetNormalized) % 1.0;
				if (newHue < 0) newHue += 1.0;

				double newSat = GrfColor.ClampDouble(originalSat + satOffset);
				double newLig = GrfColor.ClampDouble(originalLig + ligOffset);

				GrfColor newColor = GrfColor.FromHsl(newHue, newSat, newLig, originalColor.A);
				targetPalette.SetBytes(index * 4, newColor.ToRgbaBytes());

				indexPosition++;
			}
		}

		private void ApplyColorizeToIndicesGroupPreview(Pal targetPalette, Pal sourcePalette, int[] indices, PaletteGeneratorEngine.GenerationParameters parameters, int variationIndex, int totalVariations) {
			if (totalVariations <= 0) return;

			List<int> lightIndices = new List<int>();
			List<int> mediumIndices = new List<int>();
			List<int> darkIndices = new List<int>();

			foreach (int index in indices) {
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

			double hueRange = parameters.HueMax - parameters.HueMin;
			double variationHueOffset = (totalVariations > 1) ? parameters.HueMin + (hueRange / totalVariations) * variationIndex : parameters.HueMin;

			while (variationHueOffset >= 360) variationHueOffset -= 360;
			while (variationHueOffset < 0) variationHueOffset += 360;

			double primaryHue = variationHueOffset;
			double lightHueOffset = ((primaryHue + parameters.ColorizeHueLight) % 360 + 360) % 360;
			double mediumHueOffset = ((primaryHue + parameters.ColorizeHueMedium) % 360 + 360) % 360;
			double darkHueOffset = ((primaryHue + parameters.ColorizeHueDark) % 360 + 360) % 360;

			ApplyColorizeToIndices(targetPalette, sourcePalette, lightIndices, lightHueOffset, parameters.ColorizeSaturation, parameters.ColorizeBrightness);
			ApplyColorizeToIndices(targetPalette, sourcePalette, mediumIndices, mediumHueOffset, parameters.ColorizeSaturation, parameters.ColorizeBrightness);
			ApplyColorizeToIndices(targetPalette, sourcePalette, darkIndices, darkHueOffset, parameters.ColorizeSaturation, parameters.ColorizeBrightness);
		}

		private void ApplyGrayscaleToIndicesPreview(Pal targetPalette, Pal sourcePalette, int[] indices, PaletteGeneratorEngine.GenerationParameters parameters, int variationIndex, int totalVariations) {
			if (totalVariations <= 0) return;

			double tone = parameters.GrayscaleMediumTone; // Use medium tone for preview

			foreach (int index in indices) {
				if (index < 0 || index >= 256) continue;

				GrfColor originalColor = sourcePalette.GetColor(index);
				double luminance = (0.299 * originalColor.R + 0.587 * originalColor.G + 0.114 * originalColor.B) / 255.0;
				double adjustedLuminance = luminance * tone;
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


		private void _btnGenerate_Click(object sender, RoutedEventArgs e) {
			try {
				if (!ValidateInputs()) {
					return;
				}

				// Save current group if selected
				if (_selectedGroup != null) {
					SaveGroupFromUI(_selectedGroup);
				}

				SaveConfiguration();

				// Validate all groups
				List<ColorGroup> validGroups = _colorGroups.Where(g => g.IsValid()).ToList();
				if (validGroups.Count == 0) {
					MessageBox.Show(this, "No valid color groups found. Please create at least one group with valid indices.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}

				string outputFolder = _pbOutputFolder.Text;
				string fileNamePrefix = _tbFileNamePrefix.Text;

				// Get file naming parameters
				string className = _cbClass.SelectedValue != null ? _cbClass.SelectedValue.ToString() : null;
				string gender = _rbGenderFemale.IsChecked == true ? "¿©" : "³²";
				bool isCostume = _rbSpriteTypeCostume.IsChecked == true;
				int costumeNumber = isCostume ? ParseInt(_tbCostumeNumber.Text, 1) : 0;

				_btnGenerate.IsEnabled = false;
				_tbStatus.Text = "Generating palettes...";

				GrfThread.Start(() => {
					try {
						List<string> generatedFiles = _engine.GeneratePalettesWithGroups(
							_currentPalette,
							validGroups,
							outputFolder,
							fileNamePrefix,
							className,
							gender,
							isCostume,
							costumeNumber,
							_skinColorType
						);

						this.Dispatch(() => {
							_tbStatus.Text = String.Format("{0} arquivo(s) de paleta gerado(s) com sucesso", generatedFiles.Count);
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
							_tbStatus.Text = "Erro durante a geração";
							_btnGenerate.IsEnabled = true;
						});
					}
				});
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
				_tbStatus.Text = "Erro: " + err.Message;
			}
		}

		private void _btnCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private bool ValidateInputs() {
			if (_currentPalette == null) {
				MessageBox.Show(this, "Nenhuma paleta disponível. Certifique-se de que o ACT possui uma paleta válida.", "Erro de Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
				return false;
			}

			if (_colorGroups == null || _colorGroups.Count == 0) {
				MessageBox.Show(this, "Por favor, crie pelo menos um grupo de cores.", "Erro de Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
				return false;
			}

			// Validate all groups
			foreach (ColorGroup group in _colorGroups) {
				if (!group.IsValid()) {
					MessageBox.Show(this, String.Format("O grupo '{0}' é inválido. Certifique-se de que possui índices e parâmetros válidos.", group.Name), "Erro de Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
					return false;
				}
			}

			if (String.IsNullOrEmpty(_pbOutputFolder.Text)) {
				MessageBox.Show(this, "Por favor, selecione uma pasta de saída.", "Erro de Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
				return false;
			}

			if (_cbClass.SelectedValue == null || String.IsNullOrEmpty(_cbClass.SelectedValue.ToString())) {
				MessageBox.Show(this, "Por favor, selecione uma classe de personagem.", "Erro de Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
				return false;
			}

			if (_rbSpriteTypeCostume.IsChecked == true) {
				int costumeNumber = ParseInt(_tbCostumeNumber.Text, 0);
				if (costumeNumber < 1) {
					MessageBox.Show(this, "O número da fantasia deve ser maior que 0.", "Erro de Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
					return false;
				}
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

				// Load groups (already loaded in InitializeGroups)
				if (_colorGroups.Count > 0) {
					_lbGroups.SelectedItem = _colorGroups[0];
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void SaveConfiguration() {
			try {
				// Save current group if selected
				if (_selectedGroup != null) {
					SaveGroupFromUI(_selectedGroup);
				}

				// Save groups
				PaletteGeneratorConfig.SaveColorGroups(_colorGroups);

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

				// Save skin color type
				if (_cbSkinColor != null && _cbSkinColor.SelectedIndex >= 0) {
					_skinColorType = _cbSkinColor.SelectedIndex;
					PaletteGeneratorConfig.LastSkinColorType = _skinColorType;
				}

				// Selected indices are now saved as part of color groups
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

		private double GetStepForTextBox(string textBoxName) {
			// HSV Standard - Min/Max fields use corresponding Step value
			if (textBoxName == "_tbHueMin" || textBoxName == "_tbHueMax") {
				return ParseDouble(_tbHueStep.Text, 1.0);
			}
			if (textBoxName == "_tbSaturationMin" || textBoxName == "_tbSaturationMax") {
				return ParseDouble(_tbSaturationStep.Text, 0.1);
			}
			if (textBoxName == "_tbLightnessMin" || textBoxName == "_tbLightnessMax") {
				return ParseDouble(_tbLightnessStep.Text, 0.1);
			}
			
			// Colorize Mode - Hue Range Min/Max
			if (textBoxName == "_tbColorizeHueMin" || textBoxName == "_tbColorizeHueMax") {
				// Use default step for hue (1.0)
				return 1.0;
			}
			
			// For Step fields themselves, use small default step
			if (textBoxName.Contains("Step")) {
				return 0.1;
			}
			
			// For Range fields, use small step
			if (textBoxName.Contains("Range")) {
				return 1.0; // Hue range typically 0-360, so step of 1.0 is reasonable
			}
			
			// For Hue fields (but not Step/Range), use step 1.0
			if (textBoxName.Contains("Hue") && !textBoxName.Contains("Step") && !textBoxName.Contains("Range")) {
				return 1.0;
			}
			
			// Default for Saturation, Brightness, Tone, Contrast: 0.1
			return 0.1;
		}

		private void _numericTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
			// Only handle Up and Down arrow keys
			if (e.Key != Key.Up && e.Key != Key.Down) {
				return;
			}

			TextBox textBox = sender as TextBox;
			if (textBox == null) return;

			// Don't interfere if text is selected (user might be editing)
			if (textBox.SelectionLength > 0 && textBox.SelectionLength < textBox.Text.Length) {
				return;
			}

			// Get current value, use 0 as default if empty/invalid
			string currentText = textBox.Text;
			double currentValue = ParseDouble(currentText, 0.0);

			// Determine step based on modifier keys and field type
			string textBoxName = textBox.Name;
			double step = GetStepForTextBox(textBoxName);

			// Apply modifier keys for step adjustment
			if (Keyboard.Modifiers == ModifierKeys.Shift) {
				step *= 10.0; // Larger step with Shift
			}
			else if (Keyboard.Modifiers == ModifierKeys.Control) {
				step *= 0.1; // Smaller step with Ctrl
			}

			// Increment or decrement
			double newValue;
			if (e.Key == Key.Up) {
				newValue = currentValue + step;
			}
			else { // Key.Down
				newValue = currentValue - step;
			}

			// Update the TextBox with new value
			textBox.Text = newValue.ToString(CultureInfo.InvariantCulture);
			
			// Move cursor to end
			textBox.CaretIndex = textBox.Text.Length;

			// Mark event as handled to prevent default behavior
			e.Handled = true;

			// Trigger TextChanged to update the group/config
			// This is done automatically when we set Text property, but we can also manually trigger if needed
		}

		#region Color Groups Management

		private void InitializeGroups() {
			_colorGroups = PaletteGeneratorConfig.LoadColorGroups();
			if (_colorGroups.Count == 0) {
				// Create a default group
				ColorGroup defaultGroup = CreateDefaultGroup();
				_colorGroups.Add(defaultGroup);
			}
			RefreshGroupsList();
		}

		private ColorGroup CreateDefaultGroup() {
			ColorGroup group = new ColorGroup();
			group.Name = "Grupo 1";
			group.Mode = PaletteGeneratorEngine.GenerationMode.HsvStandard;
			group.NumberOfVariations = 10;
			
			// Set default parameters
			group.Parameters.HueMin = 0;
			group.Parameters.HueMax = 360;
			group.Parameters.HueStep = 10;
			group.Parameters.SaturationMin = 0;
			group.Parameters.SaturationMax = 0;
			group.Parameters.SaturationStep = 0;
			group.Parameters.LightnessMin = 0;
			group.Parameters.LightnessMax = 0;
			group.Parameters.LightnessStep = 0;
			
			return group;
		}

		private void RefreshGroupsList() {
			_lbGroups.ItemsSource = null;
			_lbGroups.ItemsSource = _colorGroups;
		}

		private void _btnAddGroup_Click(object sender, RoutedEventArgs e) {
			ColorGroup newGroup = CreateDefaultGroup();
			newGroup.Name = String.Format("Group {0}", _colorGroups.Count + 1);
			_colorGroups.Add(newGroup);
			RefreshGroupsList();
			_lbGroups.SelectedItem = newGroup;
			_tbStatus.Text = String.Format("Grupo adicionado: {0}", newGroup.Name);
		}

		private void _btnRemoveGroup_Click(object sender, RoutedEventArgs e) {
			if (_selectedGroup == null) {
				MessageBox.Show(this, "Please select a group to remove.", "No Group Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (_colorGroups.Count <= 1) {
				MessageBox.Show(this, "At least one group is required.", "Cannot Remove", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			_colorGroups.Remove(_selectedGroup);
			_selectedGroup = null;
			RefreshGroupsList();
			UpdateGroupConfigVisibility();
			_tbStatus.Text = "Group removed";
		}

		private void _btnDuplicateGroup_Click(object sender, RoutedEventArgs e) {
			if (_selectedGroup == null) {
				MessageBox.Show(this, "Por favor, selecione um grupo para duplicar.", "Nenhum Grupo Selecionado", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			ColorGroup duplicatedGroup = _selectedGroup.Clone();
			_colorGroups.Add(duplicatedGroup);
			RefreshGroupsList();
			_lbGroups.SelectedItem = duplicatedGroup;
			_tbStatus.Text = String.Format("Grupo duplicado: {0}", duplicatedGroup.Name);
		}

		private void _lbGroups_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_isUpdatingGroup) return;

			ColorGroup newSelectedGroup = _lbGroups.SelectedItem as ColorGroup;
			
			// Prevent deselection when clicking outside - restore selection if it becomes null
			if (newSelectedGroup == null && _selectedGroup != null && _colorGroups.Contains(_selectedGroup)) {
				// Restore the selection to prevent deselection when clicking outside
				_isUpdatingGroup = true;
				_lbGroups.SelectedItem = _selectedGroup;
				_isUpdatingGroup = false;
				return;
			}

			_selectedGroup = newSelectedGroup;
			UpdateGroupConfigVisibility();
			
			if (_selectedGroup != null) {
				LoadGroupToUI(_selectedGroup);
			}
		}

		private void UpdateGroupConfigVisibility() {
			bool hasSelection = _selectedGroup != null;
			_gridGroupConfig.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
			_gridGroupSelection.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
		}

		private void LoadGroupToUI(ColorGroup group) {
			if (group == null) return;

			_isUpdatingGroup = true;

			try {
				_tbGroupName.Text = group.Name;
				_tbGroupNumberOfVariations.Text = group.NumberOfVariations.ToString(CultureInfo.InvariantCulture);

				// Set mode
				if (group.Mode == PaletteGeneratorEngine.GenerationMode.HsvStandard) {
					_rbModeHsv.IsChecked = true;
				}
				else if (group.Mode == PaletteGeneratorEngine.GenerationMode.Colorize) {
					_rbModeColorize.IsChecked = true;
				}
				else {
					_rbModeGrayscale.IsChecked = true;
				}

				// Load parameters based on mode
				if (group.Mode == PaletteGeneratorEngine.GenerationMode.HsvStandard) {
					_isSyncingHueRange = true;
					_tbHueMin.Text = group.Parameters.HueMin.ToString(CultureInfo.InvariantCulture);
					_tbHueMax.Text = group.Parameters.HueMax.ToString(CultureInfo.InvariantCulture);
					_tbHueStep.Text = group.Parameters.HueStep.ToString(CultureInfo.InvariantCulture);
					double hueRange = group.Parameters.HueMax - group.Parameters.HueMin;
					_tbHueRange.Text = hueRange.ToString(CultureInfo.InvariantCulture);
					_isSyncingHueRange = false;
					_tbSaturationMin.Text = group.Parameters.SaturationMin.ToString(CultureInfo.InvariantCulture);
					_tbSaturationMax.Text = group.Parameters.SaturationMax.ToString(CultureInfo.InvariantCulture);
					_tbSaturationStep.Text = group.Parameters.SaturationStep.ToString(CultureInfo.InvariantCulture);
					_tbLightnessMin.Text = group.Parameters.LightnessMin.ToString(CultureInfo.InvariantCulture);
					_tbLightnessMax.Text = group.Parameters.LightnessMax.ToString(CultureInfo.InvariantCulture);
					_tbLightnessStep.Text = group.Parameters.LightnessStep.ToString(CultureInfo.InvariantCulture);
				}
				else if (group.Mode == PaletteGeneratorEngine.GenerationMode.Colorize) {
					_isSyncingHueRange = true;
					_tbColorizeHueMin.Text = group.Parameters.HueMin.ToString(CultureInfo.InvariantCulture);
					_tbColorizeHueMax.Text = group.Parameters.HueMax.ToString(CultureInfo.InvariantCulture);
					double colorizeHueRange = group.Parameters.HueMax - group.Parameters.HueMin;
					_tbColorizeHueRange.Text = colorizeHueRange.ToString(CultureInfo.InvariantCulture);
					_isSyncingHueRange = false;
					_tbColorizeHueLight.Text = group.Parameters.ColorizeHueLight.ToString(CultureInfo.InvariantCulture);
					_tbColorizeHueMedium.Text = group.Parameters.ColorizeHueMedium.ToString(CultureInfo.InvariantCulture);
					_tbColorizeHueDark.Text = group.Parameters.ColorizeHueDark.ToString(CultureInfo.InvariantCulture);
					_tbColorizeSaturation.Text = group.Parameters.ColorizeSaturation.ToString(CultureInfo.InvariantCulture);
					_tbColorizeBrightness.Text = group.Parameters.ColorizeBrightness.ToString(CultureInfo.InvariantCulture);
				}
				else if (group.Mode == PaletteGeneratorEngine.GenerationMode.Grayscale) {
					if (group.Parameters.GrayscaleType == PaletteGeneratorEngine.GrayscaleType.BlackWhite) {
						_rbGrayscaleTypeBlackWhite.IsChecked = true;
					}
					else if (group.Parameters.GrayscaleType == PaletteGeneratorEngine.GrayscaleType.Gray) {
						_rbGrayscaleTypeGray.IsChecked = true;
					}
					else {
						_rbGrayscaleTypeBoth.IsChecked = true;
					}
					_tbGrayscaleLightTone.Text = group.Parameters.GrayscaleLightTone.ToString(CultureInfo.InvariantCulture);
					_tbGrayscaleMediumTone.Text = group.Parameters.GrayscaleMediumTone.ToString(CultureInfo.InvariantCulture);
					_tbGrayscaleDarkTone.Text = group.Parameters.GrayscaleDarkTone.ToString(CultureInfo.InvariantCulture);
					_tbGrayscaleContrast.Text = group.Parameters.GrayscaleContrast.ToString(CultureInfo.InvariantCulture);
					_tbGrayscaleBrightness.Text = group.Parameters.GrayscaleBrightness.ToString(CultureInfo.InvariantCulture);
				}

				// Load indices
				_groupPaletteSelector.SelectedItems.Clear();
				foreach (int index in group.Indices) {
					if (index >= 0 && index < 256) {
						_groupPaletteSelector.SelectedItems.Add(index);
					}
				}
				_tbGroupIndices.Text = group.GetIndicesString();
			}
			finally {
				_isUpdatingGroup = false;
			}
		}

		private void SaveGroupFromUI(ColorGroup group) {
			if (group == null || _isUpdatingGroup) return;

			group.Name = _tbGroupName.Text;
			group.NumberOfVariations = ParseInt(_tbGroupNumberOfVariations.Text, 10);

			// Save mode
			if (_rbModeHsv.IsChecked == true) {
				group.Mode = PaletteGeneratorEngine.GenerationMode.HsvStandard;
			}
			else if (_rbModeColorize.IsChecked == true) {
				group.Mode = PaletteGeneratorEngine.GenerationMode.Colorize;
			}
			else {
				group.Mode = PaletteGeneratorEngine.GenerationMode.Grayscale;
			}

			// Save parameters based on mode
			if (group.Mode == PaletteGeneratorEngine.GenerationMode.HsvStandard) {
				group.Parameters.HueMin = ParseDouble(_tbHueMin.Text, 0);
				group.Parameters.HueMax = ParseDouble(_tbHueMax.Text, 360);
				group.Parameters.HueStep = ParseDouble(_tbHueStep.Text, 10);
				group.Parameters.SaturationMin = ParseDouble(_tbSaturationMin.Text, 0);
				group.Parameters.SaturationMax = ParseDouble(_tbSaturationMax.Text, 0);
				group.Parameters.SaturationStep = ParseDouble(_tbSaturationStep.Text, 0);
				group.Parameters.LightnessMin = ParseDouble(_tbLightnessMin.Text, 0);
				group.Parameters.LightnessMax = ParseDouble(_tbLightnessMax.Text, 0);
				group.Parameters.LightnessStep = ParseDouble(_tbLightnessStep.Text, 0);
			}
			else if (group.Mode == PaletteGeneratorEngine.GenerationMode.Colorize) {
				group.Parameters.HueMin = ParseDouble(_tbColorizeHueMin.Text, 0);
				group.Parameters.HueMax = ParseDouble(_tbColorizeHueMax.Text, 360);
				group.Parameters.ColorizeHueLight = ParseDouble(_tbColorizeHueLight.Text, 0);
				group.Parameters.ColorizeHueMedium = ParseDouble(_tbColorizeHueMedium.Text, 0);
				group.Parameters.ColorizeHueDark = ParseDouble(_tbColorizeHueDark.Text, 0);
				group.Parameters.ColorizeSaturation = ParseDouble(_tbColorizeSaturation.Text, 0);
				group.Parameters.ColorizeBrightness = ParseDouble(_tbColorizeBrightness.Text, 0);
			}
			else if (group.Mode == PaletteGeneratorEngine.GenerationMode.Grayscale) {
				if (_rbGrayscaleTypeBlackWhite.IsChecked == true) {
					group.Parameters.GrayscaleType = PaletteGeneratorEngine.GrayscaleType.BlackWhite;
				}
				else if (_rbGrayscaleTypeGray.IsChecked == true) {
					group.Parameters.GrayscaleType = PaletteGeneratorEngine.GrayscaleType.Gray;
				}
				else {
					group.Parameters.GrayscaleType = PaletteGeneratorEngine.GrayscaleType.Both;
				}
				group.Parameters.GrayscaleLightTone = ParseDouble(_tbGrayscaleLightTone.Text, 0.8);
				group.Parameters.GrayscaleMediumTone = ParseDouble(_tbGrayscaleMediumTone.Text, 0.5);
				group.Parameters.GrayscaleDarkTone = ParseDouble(_tbGrayscaleDarkTone.Text, 0.2);
				group.Parameters.GrayscaleContrast = ParseDouble(_tbGrayscaleContrast.Text, 0);
				group.Parameters.GrayscaleBrightness = ParseDouble(_tbGrayscaleBrightness.Text, 0);
			}

			// Save indices
			if (_groupPaletteSelector != null && _groupPaletteSelector.SelectedItems != null) {
				group.Indices = _groupPaletteSelector.SelectedItems.Cast<int>().ToList();
			}
			_tbGroupIndices.Text = group.GetIndicesString();

			RefreshGroupsList();
		}

		private void _tbGroupName_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
			if (_selectedGroup != null && !_isUpdatingGroup) {
				_selectedGroup.Name = _tbGroupName.Text;
				RefreshGroupsList();
			}
		}

		private void _tbGroupNumberOfVariations_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
			if (_selectedGroup != null && !_isUpdatingGroup) {
				_selectedGroup.NumberOfVariations = ParseInt(_tbGroupNumberOfVariations.Text, 10);
			}
		}

		private void _groupPaletteSelector_SelectionChanged(object sender, ObservabableListEventArgs args) {
			if (_selectedGroup != null && !_isUpdatingGroup) {
				_selectedGroup.Indices = _groupPaletteSelector.SelectedItems.Cast<int>().ToList();
				_tbGroupIndices.Text = _selectedGroup.GetIndicesString();
				_tbGroupSelectedIndices.Text = FormatSelectedIndices(_selectedGroup.Indices.ToArray());
				RefreshGroupsList();
			}
		}

		private void _btnGroupSelectAll_Click(object sender, RoutedEventArgs e) {
			if (_currentPalette == null) return;

			List<int> allIndices = Enumerable.Range(0, 256).ToList();
			_groupPaletteSelector.SelectedItems.Clear();
			foreach (int index in allIndices) {
				_groupPaletteSelector.SelectedItems.Add(index);
			}
		}

		private void _btnGroupSelectNone_Click(object sender, RoutedEventArgs e) {
			_groupPaletteSelector.SelectedItems.Clear();
		}

		private void _rbMode_Checked(object sender, RoutedEventArgs e) {
			if (_isUpdatingGroup) return;

			if (_selectedGroup != null) {
				SaveGroupFromUI(_selectedGroup);
			}

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
		}

		#endregion
	}
}
