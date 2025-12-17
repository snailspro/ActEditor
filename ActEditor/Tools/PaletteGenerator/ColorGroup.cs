using System;
using System.Collections.Generic;
using System.Linq;

namespace ActEditor.Tools.PaletteGenerator {
	/// <summary>
	/// Represents a group of palette indices with specific transformation parameters
	/// </summary>
	public class ColorGroup {
		private string _name;
		private List<int> _indices;
		private PaletteGeneratorEngine.GenerationMode _mode;
		private PaletteGeneratorEngine.GenerationParameters _parameters;
		private int _numberOfVariations;

		public ColorGroup() {
			_name = "Novo Grupo";
			_indices = new List<int>();
			_mode = PaletteGeneratorEngine.GenerationMode.HsvStandard;
			_parameters = new PaletteGeneratorEngine.GenerationParameters();
			_numberOfVariations = 10;
		}

		public ColorGroup(string name, List<int> indices, PaletteGeneratorEngine.GenerationMode mode, PaletteGeneratorEngine.GenerationParameters parameters, int numberOfVariations) {
			_name = name ?? "Novo Grupo";
			_indices = indices ?? new List<int>();
			_mode = mode;
			_parameters = parameters ?? new PaletteGeneratorEngine.GenerationParameters();
			_numberOfVariations = numberOfVariations;
		}

		/// <summary>
		/// Name identifier for this color group
		/// </summary>
		public string Name {
			get { return _name; }
			set { _name = value ?? "Novo Grupo"; }
		}

		/// <summary>
		/// List of palette indices (0-255) in this group
		/// </summary>
		public List<int> Indices {
			get { return _indices; }
			set { _indices = value ?? new List<int>(); }
		}

		/// <summary>
		/// Generation mode for this group (HSV, Colorize, or Grayscale)
		/// </summary>
		public PaletteGeneratorEngine.GenerationMode Mode {
			get { return _mode; }
			set { _mode = value; }
		}

		/// <summary>
		/// Gets a string representation of the mode (for binding)
		/// </summary>
		public string ModeString {
			get {
				switch (_mode) {
					case PaletteGeneratorEngine.GenerationMode.HsvStandard:
						return "HSV Padrão";
					case PaletteGeneratorEngine.GenerationMode.Colorize:
						return "Colorizar";
					case PaletteGeneratorEngine.GenerationMode.Grayscale:
						return "Escala de Cinza";
					default:
						return _mode.ToString();
				}
			}
		}

		/// <summary>
		/// Transformation parameters specific to this group
		/// </summary>
		public PaletteGeneratorEngine.GenerationParameters Parameters {
			get { return _parameters; }
			set { _parameters = value ?? new PaletteGeneratorEngine.GenerationParameters(); }
		}

		/// <summary>
		/// Number of variations to generate for this group
		/// </summary>
		public int NumberOfVariations {
			get { return _numberOfVariations; }
			set { _numberOfVariations = Math.Max(1, value); }
		}

		/// <summary>
		/// Creates a deep copy of this ColorGroup
		/// </summary>
		public ColorGroup Clone() {
			PaletteGeneratorEngine.GenerationParameters clonedParams = new PaletteGeneratorEngine.GenerationParameters {
				// HSV Standard mode
				HueMin = _parameters.HueMin,
				HueMax = _parameters.HueMax,
				HueStep = _parameters.HueStep,
				SaturationMin = _parameters.SaturationMin,
				SaturationMax = _parameters.SaturationMax,
				SaturationStep = _parameters.SaturationStep,
				LightnessMin = _parameters.LightnessMin,
				LightnessMax = _parameters.LightnessMax,
				LightnessStep = _parameters.LightnessStep,
				// Colorize mode
				ColorizeHueLight = _parameters.ColorizeHueLight,
				ColorizeHueMedium = _parameters.ColorizeHueMedium,
				ColorizeHueDark = _parameters.ColorizeHueDark,
				ColorizeSaturation = _parameters.ColorizeSaturation,
				ColorizeBrightness = _parameters.ColorizeBrightness,
				// Grayscale mode
				GrayscaleType = _parameters.GrayscaleType,
				GrayscaleLightTone = _parameters.GrayscaleLightTone,
				GrayscaleMediumTone = _parameters.GrayscaleMediumTone,
				GrayscaleDarkTone = _parameters.GrayscaleDarkTone,
				GrayscaleContrast = _parameters.GrayscaleContrast,
				GrayscaleBrightness = _parameters.GrayscaleBrightness
			};

			return new ColorGroup(
				_name + " (Cópia)",
				new List<int>(_indices),
				_mode,
				clonedParams,
				_numberOfVariations
			);
		}

		/// <summary>
		/// Validates that the group has valid indices and parameters
		/// </summary>
		public bool IsValid() {
			if (_indices == null || _indices.Count == 0)
				return false;

			// Validate indices are in range 0-255
			if (_indices.Any(i => i < 0 || i >= 256))
				return false;

			if (_numberOfVariations < 1)
				return false;

			return true;
		}

		/// <summary>
		/// Gets a formatted string representation of the indices (for binding)
		/// </summary>
		public string IndicesString {
			get { return GetIndicesString(); }
		}

		/// <summary>
		/// Gets a formatted string representation of the indices
		/// </summary>
		public string GetIndicesString() {
			if (_indices == null || _indices.Count == 0)
				return "";

			int[] sorted = _indices.OrderBy(i => i).ToArray();
			List<string> ranges = new List<string>();

			int start = sorted[0];
			int end = sorted[0];

			for (int i = 1; i < sorted.Length; i++) {
				if (sorted[i] == end + 1) {
					end = sorted[i];
				}
				else {
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

			if (start == end) {
				ranges.Add(start.ToString());
			}
			else {
				ranges.Add(String.Format("{0}-{1}", start, end));
			}

			return String.Join(",", ranges.ToArray());
		}
	}
}
