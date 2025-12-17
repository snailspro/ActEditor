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

		// Skin color configuration
		public static int LastSkinColorType {
			get { return Int32.Parse(ConfigAsker["[PaletteGenerator - Last skin color type]", "0"]); }
			set { ConfigAsker["[PaletteGenerator - Last skin color type]"] = value.ToString(CultureInfo.InvariantCulture); }
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

		/// <summary>
		/// Saves a list of color groups to configuration
		/// </summary>
		public static void SaveColorGroups(List<ColorGroup> groups) {
			if (groups == null || groups.Count == 0) {
				ConfigAsker["[PaletteGenerator - Color Groups Count]"] = "0";
				ConfigAsker["[PaletteGenerator - Color Groups Data]"] = "";
				return;
			}

			ConfigAsker["[PaletteGenerator - Color Groups Count]"] = groups.Count.ToString(CultureInfo.InvariantCulture);

			// Serialize groups to a simple format: group1|group2|...
			// Each group: name;mode;variations;indices;params
			// Params: hsv:hueMin,hueMax,...|colorize:hueLight,...|grayscale:type,lightTone,...
			List<string> groupStrings = new List<string>();

			foreach (ColorGroup group in groups) {
				List<string> parts = new List<string>();
				
				// Name (escape | and ;)
				parts.Add((group.Name ?? "").Replace("|", "||").Replace(";", ";;"));
				
				// Mode
				parts.Add(((int)group.Mode).ToString(CultureInfo.InvariantCulture));
				
				// Variations
				parts.Add(group.NumberOfVariations.ToString(CultureInfo.InvariantCulture));
				
				// Indices
				string indicesStr = String.Join(",", group.Indices.Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray());
				parts.Add(indicesStr);
				
				// Parameters
				List<string> paramParts = new List<string>();
				paramParts.Add(String.Format("hsv:{0},{1},{2},{3},{4},{5},{6},{7},{8}",
					group.Parameters.HueMin.ToString(CultureInfo.InvariantCulture),
					group.Parameters.HueMax.ToString(CultureInfo.InvariantCulture),
					group.Parameters.HueStep.ToString(CultureInfo.InvariantCulture),
					group.Parameters.SaturationMin.ToString(CultureInfo.InvariantCulture),
					group.Parameters.SaturationMax.ToString(CultureInfo.InvariantCulture),
					group.Parameters.SaturationStep.ToString(CultureInfo.InvariantCulture),
					group.Parameters.LightnessMin.ToString(CultureInfo.InvariantCulture),
					group.Parameters.LightnessMax.ToString(CultureInfo.InvariantCulture),
					group.Parameters.LightnessStep.ToString(CultureInfo.InvariantCulture)
				));
				paramParts.Add(String.Format("colorize:{0},{1},{2},{3},{4}",
					group.Parameters.ColorizeHueLight.ToString(CultureInfo.InvariantCulture),
					group.Parameters.ColorizeHueMedium.ToString(CultureInfo.InvariantCulture),
					group.Parameters.ColorizeHueDark.ToString(CultureInfo.InvariantCulture),
					group.Parameters.ColorizeSaturation.ToString(CultureInfo.InvariantCulture),
					group.Parameters.ColorizeBrightness.ToString(CultureInfo.InvariantCulture)
				));
				paramParts.Add(String.Format("grayscale:{0},{1},{2},{3},{4},{5}",
					((int)group.Parameters.GrayscaleType).ToString(CultureInfo.InvariantCulture),
					group.Parameters.GrayscaleLightTone.ToString(CultureInfo.InvariantCulture),
					group.Parameters.GrayscaleMediumTone.ToString(CultureInfo.InvariantCulture),
					group.Parameters.GrayscaleDarkTone.ToString(CultureInfo.InvariantCulture),
					group.Parameters.GrayscaleContrast.ToString(CultureInfo.InvariantCulture),
					group.Parameters.GrayscaleBrightness.ToString(CultureInfo.InvariantCulture)
				));
				
				parts.Add(String.Join("|", paramParts.ToArray()));
				
				groupStrings.Add(String.Join(";", parts.ToArray()));
			}

			ConfigAsker["[PaletteGenerator - Color Groups Data]"] = String.Join("||", groupStrings.ToArray());
		}

		/// <summary>
		/// Loads a list of color groups from configuration
		/// </summary>
		public static List<ColorGroup> LoadColorGroups() {
			List<ColorGroup> groups = new List<ColorGroup>();

			try {
				int count = Int32.Parse(ConfigAsker["[PaletteGenerator - Color Groups Count]", "0"]);
				if (count <= 0) {
					return groups;
				}

				string data = ConfigAsker["[PaletteGenerator - Color Groups Data]", ""];
				if (String.IsNullOrEmpty(data)) {
					return groups;
				}

				string[] groupStrings = data.Split(new string[] { "||" }, StringSplitOptions.None);
				
				foreach (string groupStr in groupStrings) {
					if (String.IsNullOrEmpty(groupStr))
						continue;

					string[] parts = groupStr.Split(';');
					if (parts.Length < 5)
						continue;

					try {
						ColorGroup group = new ColorGroup();
						
						// Name
						group.Name = parts[0].Replace(";;", ";").Replace("||", "|");
						
						// Mode
						group.Mode = (PaletteGeneratorEngine.GenerationMode)Int32.Parse(parts[1]);
						
						// Variations
						group.NumberOfVariations = Int32.Parse(parts[2]);
						
						// Indices
						if (!String.IsNullOrEmpty(parts[3])) {
							group.Indices = parts[3].Split(',').Select(s => {
								int idx;
								if (Int32.TryParse(s.Trim(), out idx) && idx >= 0 && idx < 256)
									return idx;
								return -1;
							}).Where(i => i >= 0).ToList();
						}
						
						// Parameters
						if (parts.Length >= 5 && !String.IsNullOrEmpty(parts[4])) {
							string[] paramParts = parts[4].Split('|');
							foreach (string paramPart in paramParts) {
								if (paramPart.StartsWith("hsv:")) {
									string[] hsvParams = paramPart.Substring(4).Split(',');
									if (hsvParams.Length >= 9) {
										group.Parameters.HueMin = Double.Parse(hsvParams[0].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.HueMax = Double.Parse(hsvParams[1].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.HueStep = Double.Parse(hsvParams[2].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.SaturationMin = Double.Parse(hsvParams[3].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.SaturationMax = Double.Parse(hsvParams[4].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.SaturationStep = Double.Parse(hsvParams[5].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.LightnessMin = Double.Parse(hsvParams[6].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.LightnessMax = Double.Parse(hsvParams[7].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.LightnessStep = Double.Parse(hsvParams[8].Replace(",", "."), CultureInfo.InvariantCulture);
									}
								}
								else if (paramPart.StartsWith("colorize:")) {
									string[] colorizeParams = paramPart.Substring(10).Split(',');
									if (colorizeParams.Length >= 5) {
										group.Parameters.ColorizeHueLight = Double.Parse(colorizeParams[0].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.ColorizeHueMedium = Double.Parse(colorizeParams[1].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.ColorizeHueDark = Double.Parse(colorizeParams[2].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.ColorizeSaturation = Double.Parse(colorizeParams[3].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.ColorizeBrightness = Double.Parse(colorizeParams[4].Replace(",", "."), CultureInfo.InvariantCulture);
									}
								}
								else if (paramPart.StartsWith("grayscale:")) {
									string[] grayscaleParams = paramPart.Substring(10).Split(',');
									if (grayscaleParams.Length >= 6) {
										group.Parameters.GrayscaleType = (PaletteGeneratorEngine.GrayscaleType)Int32.Parse(grayscaleParams[0]);
										group.Parameters.GrayscaleLightTone = Double.Parse(grayscaleParams[1].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.GrayscaleMediumTone = Double.Parse(grayscaleParams[2].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.GrayscaleDarkTone = Double.Parse(grayscaleParams[3].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.GrayscaleContrast = Double.Parse(grayscaleParams[4].Replace(",", "."), CultureInfo.InvariantCulture);
										group.Parameters.GrayscaleBrightness = Double.Parse(grayscaleParams[5].Replace(",", "."), CultureInfo.InvariantCulture);
									}
								}
							}
						}

						if (group.IsValid()) {
							groups.Add(group);
						}
					}
					catch {
						// Skip invalid groups
						continue;
					}
				}
			}
			catch {
				// Return empty list on error
			}

			return groups;
		}
	}
}
