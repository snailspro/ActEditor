using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ActEditor.ApplicationConfiguration;
using GRF.IO;
using Utilities;

namespace ActEditor.Tools.PaletteGenerator {
	/// <summary>
	/// Manages configuration persistence for the Palette Generator tool
	/// </summary>
	public static class PaletteGeneratorConfig {
		private static ConfigAsker ConfigAsker {
			get { return ActEditorConfiguration.ConfigAsker; }
		}

		public static string LastInputPalette {
			get { return ConfigAsker["[PaletteGenerator - Last input palette]", ""]; }
			set { ConfigAsker["[PaletteGenerator - Last input palette]"] = value; }
		}

		public static string LastOutputFolder {
			get { return ConfigAsker["[PaletteGenerator - Last output folder]", ActEditorConfiguration.ExtractingServiceLastPath]; }
			set { ConfigAsker["[PaletteGenerator - Last output folder]"] = value; }
		}

		public static string LastFileNamePrefix {
			get { return ConfigAsker["[PaletteGenerator - Last file name prefix]", "palette"]; }
			set { ConfigAsker["[PaletteGenerator - Last file name prefix]"] = value; }
		}

		public static int LastMode {
			get { return Int32.Parse(ConfigAsker["[PaletteGenerator - Last mode]", "0"]); }
			set { ConfigAsker["[PaletteGenerator - Last mode]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		// HSV Mode parameters
		public static double LastHueMin {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - HSV - Hue min]", "0"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - HSV - Hue min]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastHueMax {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - HSV - Hue max]", "360"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - HSV - Hue max]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastHueStep {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - HSV - Hue step]", "10"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - HSV - Hue step]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastHueRange {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - HSV - Hue range]", "360"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - HSV - Hue range]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastSaturationMin {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - HSV - Saturation min]", "0"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - HSV - Saturation min]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastSaturationMax {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - HSV - Saturation max]", "0"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - HSV - Saturation max]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastSaturationStep {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - HSV - Saturation step]", "0"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - HSV - Saturation step]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastLightnessMin {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - HSV - Lightness min]", "0"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - HSV - Lightness min]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastLightnessMax {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - HSV - Lightness max]", "0"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - HSV - Lightness max]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastLightnessStep {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - HSV - Lightness step]", "0"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - HSV - Lightness step]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		// Colorize Mode parameters
		public static double LastColorizeHueLight {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - Colorize - Hue light]", "0"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - Colorize - Hue light]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastColorizeHueMedium {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - Colorize - Hue medium]", "0"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - Colorize - Hue medium]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastColorizeHueDark {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - Colorize - Hue dark]", "0"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - Colorize - Hue dark]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastColorizeSaturation {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - Colorize - Saturation]", "0"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - Colorize - Saturation]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastColorizeBrightness {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - Colorize - Brightness]", "0"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - Colorize - Brightness]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static string LastSelectedIndices {
			get { return ConfigAsker["[PaletteGenerator - Selected indices]", ""]; }
			set { ConfigAsker["[PaletteGenerator - Selected indices]"] = value; }
		}

		public static int LastNumberOfVariations {
			get { return Int32.Parse(ConfigAsker["[PaletteGenerator - Number of variations]", "10"]); }
			set { ConfigAsker["[PaletteGenerator - Number of variations]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		// File naming configuration
		public static string LastSelectedClass {
			get { return ConfigAsker["[PaletteGenerator - Last selected class]", ""]; }
			set { ConfigAsker["[PaletteGenerator - Last selected class]"] = value; }
		}

		public static string LastSelectedGender {
			get { return ConfigAsker["[PaletteGenerator - Last selected gender]", "³²"]; }
			set { ConfigAsker["[PaletteGenerator - Last selected gender]"] = value; }
		}

		public static int LastSelectedSpriteType {
			get { return Int32.Parse(ConfigAsker["[PaletteGenerator - Last sprite type]", "0"]); }
			set { ConfigAsker["[PaletteGenerator - Last sprite type]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		public static int LastCostumeNumber {
			get { return Int32.Parse(ConfigAsker["[PaletteGenerator - Last costume number]", "1"]); }
			set { ConfigAsker["[PaletteGenerator - Last costume number]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		// Grayscale Mode parameters
		public static int LastGrayscaleType {
			get { return Int32.Parse(ConfigAsker["[PaletteGenerator - Grayscale - Type]", "1"]); }
			set { ConfigAsker["[PaletteGenerator - Grayscale - Type]"] = value.ToString(CultureInfo.InvariantCulture); }
		}

		public static double LastGrayscaleLightTone {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - Grayscale - Light tone]", "0.8"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - Grayscale - Light tone]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastGrayscaleMediumTone {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - Grayscale - Medium tone]", "0.5"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - Grayscale - Medium tone]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastGrayscaleDarkTone {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - Grayscale - Dark tone]", "0.2"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - Grayscale - Dark tone]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastGrayscaleContrast {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - Grayscale - Contrast]", "0"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - Grayscale - Contrast]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static double LastGrayscaleBrightness {
			get { return Double.Parse(ConfigAsker["[PaletteGenerator - Grayscale - Brightness]", "0"].Replace(",", "."), CultureInfo.InvariantCulture); }
			set { ConfigAsker["[PaletteGenerator - Grayscale - Brightness]"] = value.ToString(CultureInfo.InvariantCulture).Replace(",", "."); }
		}

		public static int[] GetSelectedIndices() {
			string indicesStr = LastSelectedIndices;
			if (String.IsNullOrEmpty(indicesStr))
				return new int[0];

			try {
				return indicesStr.Split(',').Select(s => Int32.Parse(s.Trim())).Where(i => i >= 0 && i < 256).ToArray();
			}
			catch {
				return new int[0];
			}
		}

		public static void SetSelectedIndices(int[] indices) {
			if (indices == null || indices.Length == 0) {
				LastSelectedIndices = "";
				return;
			}

			LastSelectedIndices = String.Join(",", indices.Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray());
		}
	}
}
