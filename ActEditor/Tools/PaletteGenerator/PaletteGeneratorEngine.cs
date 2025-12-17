using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GRF.FileFormats.PalFormat;
using GRF.Image;
using GRF.IO;
using ErrorManager;

namespace ActEditor.Tools.PaletteGenerator {
	/// <summary>
	/// Engine for generating palette variations in batch
	/// </summary>
	public class PaletteGeneratorEngine {
		public enum GenerationMode {
			HsvStandard = 0,
			Colorize = 1,
			Grayscale = 2
		}

		public enum GrayscaleType {
			BlackWhite = 0,
			Gray = 1,
			Both = 2
		}

		public class GenerationParameters {
			// HSV Standard mode
			public double HueMin { get; set; }
			public double HueMax { get; set; }
			public double HueStep { get; set; }
			public double SaturationMin { get; set; }
			public double SaturationMax { get; set; }
			public double SaturationStep { get; set; }
			public double LightnessMin { get; set; }
			public double LightnessMax { get; set; }
			public double LightnessStep { get; set; }

			// Colorize mode
			public double ColorizeHueLight { get; set; }
			public double ColorizeHueMedium { get; set; }
			public double ColorizeHueDark { get; set; }
			public double ColorizeSaturation { get; set; }
			public double ColorizeBrightness { get; set; }

			// Grayscale mode
			public GrayscaleType GrayscaleType { get; set; }
			public double GrayscaleLightTone { get; set; }
			public double GrayscaleMediumTone { get; set; }
			public double GrayscaleDarkTone { get; set; }
			public double GrayscaleContrast { get; set; }
			public double GrayscaleBrightness { get; set; }
		}

		/// <summary>
		/// Generates multiple palette files with variations from a Pal object
		/// </summary>
		public List<string> GeneratePalettes(
			Pal basePalette,
			int[] selectedIndices,
			GenerationMode mode,
			GenerationParameters parameters,
			string outputFolder,
			string fileNamePrefix,
			int numberOfVariations
		) {
			return GeneratePalettes(basePalette, selectedIndices, mode, parameters, outputFolder, fileNamePrefix, numberOfVariations, null, null, false, 0);
		}

		/// <summary>
		/// Generates multiple palette files with variations from a Pal object (with RO naming format)
		/// </summary>
		public List<string> GeneratePalettes(
			Pal basePalette,
			int[] selectedIndices,
			GenerationMode mode,
			GenerationParameters parameters,
			string outputFolder,
			string fileNamePrefix,
			int numberOfVariations,
			string className,
			string gender,
			bool isCostume,
			int costumeNumber
		) {
			if (basePalette == null) {
				throw new ArgumentNullException("basePalette", "Base palette cannot be null");
			}

			if (!Directory.Exists(outputFolder)) {
				Directory.CreateDirectory(outputFolder);
			}

			if (selectedIndices == null || selectedIndices.Length == 0) {
				selectedIndices = Enumerable.Range(0, 256).ToArray();
			}

			List<string> generatedFiles = new List<string>();

			if (mode == GenerationMode.HsvStandard) {
				generatedFiles = GenerateHsvStandard(basePalette, selectedIndices, parameters, outputFolder, fileNamePrefix, numberOfVariations, className, gender, isCostume, costumeNumber);
			}
			else if (mode == GenerationMode.Colorize) {
				generatedFiles = GenerateColorize(basePalette, selectedIndices, parameters, outputFolder, fileNamePrefix, numberOfVariations, className, gender, isCostume, costumeNumber);
			}
			else if (mode == GenerationMode.Grayscale) {
				generatedFiles = GenerateGrayscale(basePalette, selectedIndices, parameters, outputFolder, fileNamePrefix, numberOfVariations, className, gender, isCostume, costumeNumber);
			}

			return generatedFiles;
		}

		/// <summary>
		/// Generates multiple palette files with variations from a file path (legacy method for compatibility)
		/// </summary>
		public List<string> GeneratePalettes(
			string inputPalettePath,
			int[] selectedIndices,
			GenerationMode mode,
			GenerationParameters parameters,
			string outputFolder,
			string fileNamePrefix,
			int numberOfVariations
		) {
			if (!File.Exists(inputPalettePath)) {
				throw new FileNotFoundException("Input palette file not found: " + inputPalettePath);
			}

			byte[] basePaletteData = File.ReadAllBytes(inputPalettePath);
			if (basePaletteData.Length < 1024) {
				throw new Exception("Invalid palette file. Expected 1024 bytes, got " + basePaletteData.Length);
			}

			Pal basePalette = new Pal(basePaletteData);
			return GeneratePalettes(basePalette, selectedIndices, mode, parameters, outputFolder, fileNamePrefix, numberOfVariations);
		}

		/// <summary>
		/// Generates multiple palette files combining transformations from multiple color groups
		/// </summary>
		public List<string> GeneratePalettesWithGroups(
			Pal basePalette,
			List<ColorGroup> groups,
			string outputFolder,
			string fileNamePrefix,
			string className = null,
			string gender = null,
			bool isCostume = false,
			int costumeNumber = 0,
			int skinColorType = 0
		) {
			if (basePalette == null) {
				throw new ArgumentNullException("basePalette", "Base palette cannot be null");
			}

			if (groups == null || groups.Count == 0) {
				throw new ArgumentException("At least one color group is required", "groups");
			}

			// Validate all groups
			foreach (ColorGroup group in groups) {
				if (!group.IsValid()) {
					throw new ArgumentException(String.Format("Invalid color group: {0}", group.Name), "groups");
				}
			}

			if (!Directory.Exists(outputFolder)) {
				Directory.CreateDirectory(outputFolder);
			}

			List<string> generatedFiles = new List<string>();

			// Calculate maximum number of variations across all groups
			int maxVariations = groups.Max(g => g.NumberOfVariations);

			// Generate palettes
			for (int variationIndex = 0; variationIndex < maxVariations; variationIndex++) {
				// Create a copy of the base palette
				byte[] paletteData = new byte[1024];
				Buffer.BlockCopy(basePalette.BytePalette, 0, paletteData, 0, 1024);
				Pal newPalette = new Pal(paletteData);

				// Apply transformations from each group first
				foreach (ColorGroup group in groups) {
					// Calculate which variation to use for this group
					// If group has fewer variations, cycle through them
					int groupVariationIndex = variationIndex % group.NumberOfVariations;

					// Apply group transformation
					ApplyGroupTransformation(newPalette, basePalette, group, groupVariationIndex);
				}

				// Apply skin color AFTER group transformations to ensure it's not overwritten
				ApplySkinColor(newPalette, skinColorType);

				// Copy modified palette data back to array before writing
				Buffer.BlockCopy(newPalette.BytePalette, 0, paletteData, 0, 1024);

				// Ensure transparency byte is 0
				paletteData[3] = 0;

				// Generate filename (start from 1, not 0)
				string fileName = GenerateFileName(outputFolder, fileNamePrefix, variationIndex + 1, className, gender, isCostume, costumeNumber);
				File.WriteAllBytes(fileName, paletteData);
				generatedFiles.Add(fileName);
			}

			return generatedFiles;
		}

		/// <summary>
		/// Applies skin color to indices 32-39
		/// </summary>
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
				// Black skin colors (applied to both sets of indices) - using hex strings to ensure correct RGB order
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

		/// <summary>
		/// Applies a single group's transformation to a palette for a specific variation index
		/// </summary>
		private void ApplyGroupTransformation(Pal targetPalette, Pal sourcePalette, ColorGroup group, int variationIndex) {
			if (group == null || !group.IsValid() || group.Indices == null || group.Indices.Count == 0) {
				return;
			}

			// Exclude skin color indices (32-39 and 128-135) from group transformations
			int[] skinIndices = { 32, 33, 34, 35, 36, 37, 38, 39, 128, 129, 130, 131, 132, 133, 134, 135 };
			HashSet<int> skinIndicesSet = new HashSet<int>(skinIndices);
			int[] indices = group.Indices.Where(idx => !skinIndicesSet.Contains(idx)).ToArray();

			if (group.Mode == GenerationMode.HsvStandard) {
				ApplyHsvStandardToIndices(targetPalette, sourcePalette, indices, group.Parameters, variationIndex, group.NumberOfVariations);
			}
			else if (group.Mode == GenerationMode.Colorize) {
				ApplyColorizeToIndicesGroup(targetPalette, sourcePalette, indices, group.Parameters, variationIndex, group.NumberOfVariations);
			}
			else if (group.Mode == GenerationMode.Grayscale) {
				ApplyGrayscaleToIndices(targetPalette, sourcePalette, indices, group.Parameters, variationIndex, group.NumberOfVariations);
			}
		}

		/// <summary>
		/// Applies HSV Standard transformation to specific indices for a variation
		/// </summary>
		private void ApplyHsvStandardToIndices(Pal targetPalette, Pal sourcePalette, int[] indices, GenerationParameters parameters, int variationIndex, int totalVariations) {
			if (totalVariations <= 0) return;

			double hueRange = parameters.HueMax - parameters.HueMin;
			double satOffset = parameters.SaturationMin;
			double ligOffset = parameters.LightnessMin;

			// Calculate hue portion for each selected index
			int numSelectedIndices = indices.Length;
			double huePortionPerIndex = (numSelectedIndices > 0) ? hueRange / numSelectedIndices : 0;

			// Calculate variation shift for this variation
			double variationShift = (totalVariations > 1) ? (hueRange / totalVariations) * variationIndex : 0;

			int indexPosition = 0;
			foreach (int index in indices) {
				if (index < 0 || index >= 256) continue;

				GrfColor originalColor = sourcePalette.GetColor(index);
				double originalHue = originalColor.Hue;
				double originalSat = originalColor.Hsl.S;
				double originalLig = originalColor.Lightness;

				// Calculate hue offset for this index
				double baseHueOffsetForIndex = huePortionPerIndex * indexPosition;
				double combinedOffset = baseHueOffsetForIndex + variationShift;

				// Wrap around the range if needed
				while (combinedOffset >= hueRange) combinedOffset -= hueRange;
				while (combinedOffset < 0) combinedOffset += hueRange;

				double finalHueOffsetDegrees = combinedOffset + parameters.HueMin;
				double hueOffsetNormalized = finalHueOffsetDegrees / 360.0;
				double newHue = (originalHue + hueOffsetNormalized) % 1.0;
				if (newHue < 0) newHue += 1.0;

				// Apply saturation and lightness with smoother scaling (divide by 10 for finer control)
				// Using multiplication: new = original * (1.0 + offset/10.0) for smoother control
				double newSat = GrfColor.ClampDouble(originalSat * (1.0 + satOffset / 10.0));
				double newLig = GrfColor.ClampDouble(originalLig * (1.0 + ligOffset / 10.0));

				GrfColor newColor = GrfColor.FromHsl(newHue, newSat, newLig, originalColor.A);
				targetPalette.SetBytes(index * 4, newColor.ToRgbaBytes());

				indexPosition++;
			}
		}

		/// <summary>
		/// Applies Colorize transformation to specific indices for a variation
		/// </summary>
		private void ApplyColorizeToIndicesGroup(Pal targetPalette, Pal sourcePalette, int[] indices, GenerationParameters parameters, int variationIndex, int totalVariations) {
			if (totalVariations <= 0) return;

			// Classify colors into light, medium, and dark based on lightness
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

			// Calculate hue range for variations
			double hueRange = parameters.HueMax - parameters.HueMin;

			// Calculate hue variation offset for this palette
			double variationHueOffset = (totalVariations > 1) ? parameters.HueMin + (hueRange / totalVariations) * variationIndex : parameters.HueMin;

			// Ensure offset stays within valid range (0-360)
			while (variationHueOffset >= 360) variationHueOffset -= 360;
			while (variationHueOffset < 0) variationHueOffset += 360;

			// Use variationHueOffset as the primary base hue for this variation
			double primaryHue = variationHueOffset;

			// Apply relative offsets
			double lightHueOffset = (primaryHue + parameters.ColorizeHueLight);
			double mediumHueOffset = (primaryHue + parameters.ColorizeHueMedium);
			double darkHueOffset = (primaryHue + parameters.ColorizeHueDark);

			// Normalize to 0-360 range
			lightHueOffset = ((lightHueOffset % 360) + 360) % 360;
			mediumHueOffset = ((mediumHueOffset % 360) + 360) % 360;
			darkHueOffset = ((darkHueOffset % 360) + 360) % 360;

			// Apply colorize transformations with varied hue offsets
			ApplyColorizeToIndices(targetPalette, sourcePalette, lightIndices, lightHueOffset, parameters.ColorizeSaturation, parameters.ColorizeBrightness);
			ApplyColorizeToIndices(targetPalette, sourcePalette, mediumIndices, mediumHueOffset, parameters.ColorizeSaturation, parameters.ColorizeBrightness);
			ApplyColorizeToIndices(targetPalette, sourcePalette, darkIndices, darkHueOffset, parameters.ColorizeSaturation, parameters.ColorizeBrightness);
		}

		/// <summary>
		/// Applies Grayscale transformation to specific indices for a variation
		/// </summary>
		private void ApplyGrayscaleToIndices(Pal targetPalette, Pal sourcePalette, int[] indices, GenerationParameters parameters, int variationIndex, int totalVariations) {
			if (totalVariations <= 0) return;

			// Determine which tone to use based on variation index
			// Cycle through light, medium, dark tones
			double tone;
			int toneIndex = variationIndex % 3;
			if (toneIndex == 0) {
				tone = parameters.GrayscaleLightTone;
			}
			else if (toneIndex == 1) {
				tone = parameters.GrayscaleMediumTone;
			}
			else {
				tone = parameters.GrayscaleDarkTone;
			}

			// Apply grayscale conversion to selected indices
			foreach (int index in indices) {
				if (index < 0 || index >= 256) continue;

				GrfColor originalColor = sourcePalette.GetColor(index);
				GrfColor grayscaleColor = ConvertToGrayscale(
					originalColor,
					tone,
					parameters.GrayscaleType,
					parameters.GrayscaleContrast,
					parameters.GrayscaleBrightness
				);

				targetPalette.SetBytes(index * 4, grayscaleColor.ToRgbaBytes());
			}
		}

		private List<string> GenerateHsvStandard(
			Pal basePalette,
			int[] selectedIndices,
			GenerationParameters parameters,
			string outputFolder,
			string fileNamePrefix,
			int numberOfVariations,
			string className = null,
			string gender = null,
			bool isCostume = false,
			int costumeNumber = 0
		) {
			List<string> generatedFiles = new List<string>();

			// Calculate total number of variations based on ranges and steps
			int hueSteps = (int)Math.Max(1, Math.Ceiling((parameters.HueMax - parameters.HueMin) / Math.Max(0.1, parameters.HueStep)));
			int satSteps = (int)Math.Max(1, Math.Ceiling((parameters.SaturationMax - parameters.SaturationMin) / Math.Max(0.01, parameters.SaturationStep)));
			int ligSteps = (int)Math.Max(1, Math.Ceiling((parameters.LightnessMax - parameters.LightnessMin) / Math.Max(0.01, parameters.LightnessStep)));

			int variationIndex = 0;

			// If numberOfVariations is specified, generate variations by distributing hue across the range
			if (numberOfVariations > 0) {
				double hueRange = parameters.HueMax - parameters.HueMin;
				double satOffset = parameters.SaturationMin;
				double ligOffset = parameters.LightnessMin;

				// Calculate hue portion for each selected index
				int numSelectedIndices = selectedIndices.Length;
				double huePortionPerIndex = (numSelectedIndices > 0) ? hueRange / numSelectedIndices : 0;

				for (int i = 0; i < numberOfVariations; i++) {
					// Calculate base shift for this variation (rotates the distribution)
					double variationShift = (numberOfVariations > 1) ? (hueRange / numberOfVariations) * i : 0;

					// Create a copy of the base palette
					byte[] paletteData = new byte[1024];
					Buffer.BlockCopy(basePalette.BytePalette, 0, paletteData, 0, 1024);

					Pal newPalette = new Pal(paletteData);

					// Apply transformations to selected indices with distributed hue
					int indexPosition = 0;
					foreach (int index in selectedIndices) {
						if (index < 0 || index >= 256) continue;

						GrfColor originalColor = basePalette.GetColor(index);
						double originalHue = originalColor.Hue;
						double originalSat = originalColor.Hsl.S;
						double originalLig = originalColor.Lightness;

						// Calculate hue offset for this index: distribute range among indices, then add variation shift
						// Each index gets a portion of the range based on its position within the selected indices
						// Example: 3 indices, range 360: index 0 gets 0, index 1 gets 120, index 2 gets 240
						double baseHueOffsetForIndex = huePortionPerIndex * indexPosition;
						
						// Add variation shift to rotate the distribution across different palettes
						// This makes each palette have a different color distribution
						double combinedOffset = baseHueOffsetForIndex + variationShift;
						
						// Wrap around the range if needed (keep within 0 to hueRange)

						// Use proper wrap logic for floating point
						while (combinedOffset >= hueRange) combinedOffset -= hueRange;
						while (combinedOffset < 0) combinedOffset += hueRange;
						
						// Add the minimum to get the absolute hue offset in degrees (0-360)
						// This shifts the entire distribution to start from HueMin
						double finalHueOffsetDegrees = combinedOffset + parameters.HueMin;

						// Apply hue offset (Hue is normalized 0-1 in GrfColor, so convert degrees to normalized)
						double hueOffsetNormalized = finalHueOffsetDegrees / 360.0;
						double newHue = (originalHue + hueOffsetNormalized) % 1.0;
						if (newHue < 0) newHue += 1.0;

						double newSat = GrfColor.ClampDouble(originalSat + satOffset);
						double newLig = GrfColor.ClampDouble(originalLig + ligOffset);

						GrfColor newColor = GrfColor.FromHsl(newHue, newSat, newLig, originalColor.A);
						newPalette.SetBytes(index * 4, newColor.ToRgbaBytes());

						indexPosition++;
					}

					// Copy modified palette data back to array before writing
					Buffer.BlockCopy(newPalette.BytePalette, 0, paletteData, 0, 1024);

					// Ensure transparency byte is 0
					paletteData[3] = 0;

					// Generate filename (start from 1, not 0)
					string fileName = GenerateFileName(outputFolder, fileNamePrefix, variationIndex + 1, className, gender, isCostume, costumeNumber);
					File.WriteAllBytes(fileName, paletteData);
					generatedFiles.Add(fileName);

					variationIndex++;
				}

				return generatedFiles;
			}

			// Original logic for when numberOfVariations is 0 (use steps)
			double effectiveHueStep = parameters.HueStep;

			for (int h = 0; h < hueSteps; h++) {
				double hueOffset = parameters.HueMin + (h * effectiveHueStep);
				if (hueOffset > parameters.HueMax && parameters.HueStep > 0) break;

				for (int s = 0; s < satSteps; s++) {
					double satOffset = parameters.SaturationMin + (s * parameters.SaturationStep);
					if (satOffset > parameters.SaturationMax && parameters.SaturationStep > 0) break;

					for (int l = 0; l < ligSteps; l++) {
						double ligOffset = parameters.LightnessMin + (l * parameters.LightnessStep);
						if (ligOffset > parameters.LightnessMax && parameters.LightnessStep > 0) break;

						// Create a copy of the base palette
						byte[] paletteData = new byte[1024];
						Buffer.BlockCopy(basePalette.BytePalette, 0, paletteData, 0, 1024);

						Pal newPalette = new Pal(paletteData);

						// Apply transformations to selected indices
						foreach (int index in selectedIndices) {
							if (index < 0 || index >= 256) continue;

							GrfColor originalColor = basePalette.GetColor(index);
							double originalHue = originalColor.Hue;
							double originalSat = originalColor.Hsl.S;
							double originalLig = originalColor.Lightness;

							// Apply offsets (normalized: hue is 0-1, saturation/lightness are 0-1)
							double newHue = (originalHue + hueOffset / 360.0) % 1.0;
							if (newHue < 0) newHue += 1.0;

							double newSat = GrfColor.ClampDouble(originalSat + satOffset);
							double newLig = GrfColor.ClampDouble(originalLig + ligOffset);

							GrfColor newColor = GrfColor.FromHsl(newHue, newSat, newLig, originalColor.A);
							newPalette.SetBytes(index * 4, newColor.ToRgbaBytes());
						}

						// Copy modified palette data back to array before writing
						Buffer.BlockCopy(newPalette.BytePalette, 0, paletteData, 0, 1024);

						// Ensure transparency byte is 0
						paletteData[3] = 0;

						// Generate filename (start from 1, not 0)
						string fileName = GenerateFileName(outputFolder, fileNamePrefix, variationIndex + 1);
						File.WriteAllBytes(fileName, paletteData);
						generatedFiles.Add(fileName);

						variationIndex++;
					}
				}
			}

			return generatedFiles;
		}

		private List<string> GenerateColorize(
			Pal basePalette,
			int[] selectedIndices,
			GenerationParameters parameters,
			string outputFolder,
			string fileNamePrefix,
			int numberOfVariations,
			string className = null,
			string gender = null,
			bool isCostume = false,
			int costumeNumber = 0
		) {
			List<string> generatedFiles = new List<string>();

			// Classify colors into light, medium, and dark based on lightness
			List<int> lightIndices = new List<int>();
			List<int> mediumIndices = new List<int>();
			List<int> darkIndices = new List<int>();

			foreach (int index in selectedIndices) {
				if (index < 0 || index >= 256) continue;

				GrfColor color = basePalette.GetColor(index);
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

			// Calculate hue range for variations
			double hueRange = parameters.HueMax - parameters.HueMin;

			// Generate variations with hue range distribution
			for (int i = 0; i < numberOfVariations; i++) {
				// Calculate hue variation offset for this palette
				// Distribute the hue range across the number of variations
				// Each variation should cover a different portion of the hue spectrum
				// For 360 variations with range 0-360, each gets 1 degree step
				double variationHueOffset = (numberOfVariations > 1) ? parameters.HueMin + (hueRange / numberOfVariations) * i : parameters.HueMin;
				
				// Ensure offset stays within valid range (0-360)
				while (variationHueOffset >= 360) variationHueOffset -= 360;
				while (variationHueOffset < 0) variationHueOffset += 360;
				
				double primaryHue = variationHueOffset;
				
				// Apply relative offsets: if base values are 0, all tones use the same hue
				// If base values differ, they create subtle hue differences between light/medium/dark
				double lightHueOffset = (primaryHue + parameters.ColorizeHueLight);
				double mediumHueOffset = (primaryHue + parameters.ColorizeHueMedium);
				double darkHueOffset = (primaryHue + parameters.ColorizeHueDark);
				
				// Normalize to 0-360 range
				lightHueOffset = ((lightHueOffset % 360) + 360) % 360;
				mediumHueOffset = ((mediumHueOffset % 360) + 360) % 360;
				darkHueOffset = ((darkHueOffset % 360) + 360) % 360;

				// Create a copy of the base palette
				byte[] paletteData = new byte[1024];
				Buffer.BlockCopy(basePalette.BytePalette, 0, paletteData, 0, 1024);

				Pal newPalette = new Pal(paletteData);

				// Apply colorize transformations with varied hue offsets
				ApplyColorizeToIndices(newPalette, basePalette, lightIndices, lightHueOffset, parameters.ColorizeSaturation, parameters.ColorizeBrightness);
				ApplyColorizeToIndices(newPalette, basePalette, mediumIndices, mediumHueOffset, parameters.ColorizeSaturation, parameters.ColorizeBrightness);
				ApplyColorizeToIndices(newPalette, basePalette, darkIndices, darkHueOffset, parameters.ColorizeSaturation, parameters.ColorizeBrightness);

				// Copy modified palette data back to array before writing
				Buffer.BlockCopy(newPalette.BytePalette, 0, paletteData, 0, 1024);

				// Ensure transparency byte is 0
				paletteData[3] = 0;

				// Generate filename (start from 1, not 0)
				string fileName = GenerateFileName(outputFolder, fileNamePrefix, i + 1, className, gender, isCostume, costumeNumber);
				File.WriteAllBytes(fileName, paletteData);
				generatedFiles.Add(fileName);
			}

			return generatedFiles;
		}

		private List<string> GenerateGrayscale(
			Pal basePalette,
			int[] selectedIndices,
			GenerationParameters parameters,
			string outputFolder,
			string fileNamePrefix,
			int numberOfVariations,
			string className = null,
			string gender = null,
			bool isCostume = false,
			int costumeNumber = 0
		) {
			List<string> generatedFiles = new List<string>();
			int fileIndex = 1;

			// Determine which tones to generate based on GrayscaleType
			List<double> tonesToGenerate = new List<double>();
			
			if (parameters.GrayscaleType == GrayscaleType.BlackWhite || parameters.GrayscaleType == GrayscaleType.Both) {
				// For BlackWhite, we still use the tone values but will force to 0.0 or 1.0 in conversion
				tonesToGenerate.Add(parameters.GrayscaleLightTone);
				tonesToGenerate.Add(parameters.GrayscaleMediumTone);
				tonesToGenerate.Add(parameters.GrayscaleDarkTone);
			}
			
			if (parameters.GrayscaleType == GrayscaleType.Gray || parameters.GrayscaleType == GrayscaleType.Both) {
				// Add gray tones if not already added
				if (parameters.GrayscaleType == GrayscaleType.Gray) {
					tonesToGenerate.Add(parameters.GrayscaleLightTone);
					tonesToGenerate.Add(parameters.GrayscaleMediumTone);
					tonesToGenerate.Add(parameters.GrayscaleDarkTone);
				}
			}

			// Generate variations
			for (int variation = 0; variation < numberOfVariations; variation++) {
				// Generate one file for each tone
				foreach (double tone in tonesToGenerate) {
					// Create a copy of the base palette
					byte[] paletteData = new byte[1024];
					Buffer.BlockCopy(basePalette.BytePalette, 0, paletteData, 0, 1024);

					Pal newPalette = new Pal(paletteData);

					// Apply grayscale conversion to selected indices
					foreach (int index in selectedIndices) {
						if (index < 0 || index >= 256) continue;

						GrfColor originalColor = basePalette.GetColor(index);
						GrfColor grayscaleColor = ConvertToGrayscale(
							originalColor,
							tone,
							parameters.GrayscaleType,
							parameters.GrayscaleContrast,
							parameters.GrayscaleBrightness
						);

						newPalette.SetBytes(index * 4, grayscaleColor.ToRgbaBytes());
					}

					// Copy modified palette data back to array before writing
					Buffer.BlockCopy(newPalette.BytePalette, 0, paletteData, 0, 1024);

					// Ensure transparency byte is 0
					paletteData[3] = 0;

					// Generate filename
					string fileName = GenerateFileName(outputFolder, fileNamePrefix, fileIndex, className, gender, isCostume, costumeNumber);
					File.WriteAllBytes(fileName, paletteData);
					generatedFiles.Add(fileName);

					fileIndex++;
				}
			}

			return generatedFiles;
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
				
				// Use target hue directly, preserving the lightness structure but changing color
				// This is how Colorize works - it replaces the hue while maintaining the lightness pattern
				double newHue = targetHue;

				// Apply saturation and brightness with smoother scaling (divide by 10 for finer control)
				// Using multiplication: new = original * (1.0 + offset/10.0) for smoother control
				double newSat = GrfColor.ClampDouble(originalSat * (1.0 + saturationOffset / 10.0));
				double newLig = GrfColor.ClampDouble(originalLig * (1.0 + brightnessOffset / 10.0));

				GrfColor newColor = GrfColor.FromHsl(newHue, newSat, newLig, originalColor.A);
				targetPalette.SetBytes(index * 4, newColor.ToRgbaBytes());
			}
		}

		public GrfColor ConvertToGrayscale(GrfColor originalColor, double tone, GrayscaleType grayscaleType, double contrast, double brightness) {
			// Calculate luminance using standard formula: 0.299*R + 0.587*G + 0.114*B
			double luminance = (0.299 * originalColor.R + 0.587 * originalColor.G + 0.114 * originalColor.B) / 255.0;

			// Apply tone multiplier
			double adjustedLuminance = luminance * tone;

			// Apply contrast: (value - 0.5) * (1 + contrast) + 0.5
			adjustedLuminance = (adjustedLuminance - 0.5) * (1.0 + contrast) + 0.5;

			// Apply brightness with smoother scaling (divide by 10 for finer control)
			// Using addition but with scaled offset: new = value + brightness/10.0
			adjustedLuminance = adjustedLuminance + brightness / 10.0;

			// Clamp between 0.0 and 1.0
			adjustedLuminance = GrfColor.ClampDouble(adjustedLuminance);

			// Apply grayscale type
			if (grayscaleType == GrayscaleType.BlackWhite) {
				// Force to pure black (0.0) or white (1.0) based on threshold
				adjustedLuminance = adjustedLuminance > 0.5 ? 1.0 : 0.0;
			}
			// For Gray and Both, keep the adjusted luminance value

			// Convert to RGB (grayscale = same value for R, G, B)
			byte grayValue = (byte)(adjustedLuminance * 255);
			return new GrfColor(grayValue, grayValue, grayValue, originalColor.A);
		}

		private string GenerateFileName(string outputFolder, string prefix, int index, string className = null, string gender = null, bool isCostume = false, int costumeNumber = 0) {
			string fileName;

			// Use RO naming format if className and gender are provided
			if (!String.IsNullOrEmpty(className) && !String.IsNullOrEmpty(gender)) {
				if (isCostume) {
					// Costume format: {className}_{gender}_{variation}_{costumeNumber}.pal
					// Variation starts at 300
					int variationNumber = 300 + (index - 1);
					fileName = GrfPath.Combine(outputFolder, String.Format("{0}_{1}_{2}_{3}.pal", className, gender, variationNumber, costumeNumber));
				}
				else {
					// Default format: {className}_{gender}_1_{variation}.pal
					// Variation starts at 1
					fileName = GrfPath.Combine(outputFolder, String.Format("{0}_{1}_1_{2}.pal", className, gender, index));
				}
			}
			else {
				// Legacy format: {prefix}_{index:000}.pal
				fileName = GrfPath.Combine(outputFolder, String.Format("{0}_{1:000}.pal", prefix, index));
			}

			// Ensure we don't overwrite existing files
			string originalFileName = fileName;
			int counter = 0;
			while (File.Exists(fileName)) {
				counter++;
				if (!String.IsNullOrEmpty(className) && !String.IsNullOrEmpty(gender)) {
					// For RO format, append counter before .pal
					string nameWithoutExt = originalFileName.Substring(0, originalFileName.Length - 4);
					fileName = String.Format("{0}_{1}.pal", nameWithoutExt, counter);
				}
				else {
					fileName = GrfPath.Combine(outputFolder, String.Format("{0}_{1:000}_{2}.pal", prefix, index, counter));
				}
			}

			return fileName;
		}
	}
}
