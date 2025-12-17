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

			// #region agent log
			try {
				using (System.IO.StreamWriter logFile = System.IO.File.AppendText(@"c:\GitHub\ActEditor\.cursor\debug.log")) {
					logFile.WriteLine(String.Format("{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"A\",\"location\":\"GenerateColorize:282\",\"message\":\"Index classification\",\"data\":{{\"selectedCount\":{0},\"lightCount\":{1},\"mediumCount\":{2},\"darkCount\":{3}}},\"timestamp\":{4}}}", selectedIndices.Length, lightIndices.Count, mediumIndices.Count, darkIndices.Count, (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds));
				}
			} catch { }
			// #endregion

			// Calculate hue range for variations
			double hueRange = parameters.HueMax - parameters.HueMin;

			// #region agent log
			try {
				using (System.IO.StreamWriter logFile = System.IO.File.AppendText(@"c:\GitHub\ActEditor\.cursor\debug.log")) {
					logFile.WriteLine(String.Format("{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"GenerateColorize:285\",\"message\":\"Hue range calculation\",\"data\":{{\"hueMin\":{0},\"hueMax\":{1},\"hueRange\":{2}}},\"timestamp\":{3}}}", parameters.HueMin, parameters.HueMax, hueRange, (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds));
				}
			} catch { }
			// #endregion

			// Generate variations with hue range distribution
			for (int i = 0; i < numberOfVariations; i++) {
				// Calculate hue variation offset for this palette
				// Distribute the hue range across the number of variations
				// Each variation should cover a different portion of the hue spectrum
				// For 360 variations with range 0-360, each gets 1 degree step
				double variationHueOffset = (numberOfVariations > 1) ? parameters.HueMin + (hueRange / numberOfVariations) * i : parameters.HueMin;
				
				// #region agent log
				try {
					using (System.IO.StreamWriter logFile = System.IO.File.AppendText(@"c:\GitHub\ActEditor\.cursor\debug.log")) {
						logFile.WriteLine(String.Format("{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"F\",\"location\":\"GenerateColorize:310\",\"message\":\"Variation calculation\",\"data\":{{\"variation\":{0},\"hueMin\":{1},\"hueMax\":{2},\"hueRange\":{3},\"numberOfVariations\":{4},\"calculatedOffset\":{5}}},\"timestamp\":{6}}}", i, parameters.HueMin, parameters.HueMax, hueRange, numberOfVariations, variationHueOffset, (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds));
					}
				} catch { }
				// #endregion
				
				// Ensure offset stays within valid range (0-360)
				while (variationHueOffset >= 360) variationHueOffset -= 360;
				while (variationHueOffset < 0) variationHueOffset += 360;
				
				// Apply the variation offset to each colorize hue parameter
				// For Colorize mode variations, each palette should use a different hue across the full spectrum
				// The variationHueOffset varies from 0 to 360, covering the entire hue circle
				// We use this as the PRIMARY hue for this palette variation
				// The ColorizeHueLight/Medium/Dark values are RELATIVE offsets to differentiate tones
				
				// Use variationHueOffset as the primary base hue for this variation
				// Add small relative offsets for light/medium/dark to create subtle variations within the same palette
				// This ensures each of the 360 palettes covers a different primary hue (0°, 1°, 2°... 359°)
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

				// #region agent log
				try {
					using (System.IO.StreamWriter logFile = System.IO.File.AppendText(@"c:\GitHub\ActEditor\.cursor\debug.log")) {
						logFile.WriteLine(String.Format("{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\",\"location\":\"GenerateColorize:295\",\"message\":\"Variation offsets\",\"data\":{{\"variation\":{0},\"variationHueOffset\":{1},\"lightHueOffset\":{2},\"mediumHueOffset\":{3},\"darkHueOffset\":{4}}},\"timestamp\":{5}}}", i, variationHueOffset, lightHueOffset, mediumHueOffset, darkHueOffset, (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds));
					}
				} catch { }
				// #endregion

				// Create a copy of the base palette
				byte[] paletteData = new byte[1024];
				Buffer.BlockCopy(basePalette.BytePalette, 0, paletteData, 0, 1024);

				Pal newPalette = new Pal(paletteData);

				// #region agent log
				byte[] beforeBytes = new byte[16];
				Buffer.BlockCopy(paletteData, 0, beforeBytes, 0, 16);
				// #endregion

				// Apply colorize transformations with varied hue offsets
				ApplyColorizeToIndices(newPalette, basePalette, lightIndices, lightHueOffset, parameters.ColorizeSaturation, parameters.ColorizeBrightness);
				ApplyColorizeToIndices(newPalette, basePalette, mediumIndices, mediumHueOffset, parameters.ColorizeSaturation, parameters.ColorizeBrightness);
				ApplyColorizeToIndices(newPalette, basePalette, darkIndices, darkHueOffset, parameters.ColorizeSaturation, parameters.ColorizeBrightness);

				// #region agent log
				byte[] afterPaletteBytes = new byte[16];
				byte[] afterDataBytes = new byte[16];
				Buffer.BlockCopy(newPalette.BytePalette, 0, afterPaletteBytes, 0, 16);
				Buffer.BlockCopy(paletteData, 0, afterDataBytes, 0, 16);
				bool dataMatches = true;
				for (int j = 0; j < 1024 && dataMatches; j++) {
					if (paletteData[j] != newPalette.BytePalette[j]) dataMatches = false;
				}
				try {
					using (System.IO.StreamWriter logFile = System.IO.File.AppendText(@"c:\GitHub\ActEditor\.cursor\debug.log")) {
						logFile.WriteLine(String.Format("{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"D\",\"location\":\"GenerateColorize:308\",\"message\":\"After SetBytes comparison\",\"data\":{{\"variation\":{0},\"dataMatches\":{1},\"beforeFirst4Bytes\":\"{2}\",\"paletteFirst4Bytes\":\"{3}\",\"dataFirst4Bytes\":\"{4}\"}},\"timestamp\":{5}}}", i, dataMatches.ToString().ToLower(), BitConverter.ToString(beforeBytes), BitConverter.ToString(afterPaletteBytes), BitConverter.ToString(afterDataBytes), (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds));
					}
				} catch { }
				// #endregion

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
			// #region agent log
			int processedCount = 0;
			// #endregion
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
				
				// #region agent log
				if (processedCount == 0 && indices.Count > 0) {
					try {
						using (System.IO.StreamWriter logFile = System.IO.File.AppendText(@"c:\GitHub\ActEditor\.cursor\debug.log")) {
							logFile.WriteLine(String.Format("{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"H\",\"location\":\"ApplyColorizeToIndices:412\",\"message\":\"Colorize hue replacement\",\"data\":{{\"index\":{0},\"originalHue\":{1},\"targetHueDegrees\":{2},\"targetHueNormalized\":{3},\"newHue\":{4}}},\"timestamp\":{5}}}", index, originalHue, hueOffset, targetHue, newHue, (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds));
						}
					} catch { }
				}
				// #endregion

				// Apply saturation and brightness offsets
				// Keep original saturation/brightness structure, but apply offsets
				double newSat = GrfColor.ClampDouble(originalSat + saturationOffset);
				double newLig = GrfColor.ClampDouble(originalLig + brightnessOffset);

				GrfColor newColor = GrfColor.FromHsl(newHue, newSat, newLig, originalColor.A);
				targetPalette.SetBytes(index * 4, newColor.ToRgbaBytes());
				// #region agent log
				processedCount++;
				if (processedCount <= 3) {
					byte[] rgba = newColor.ToRgbaBytes();
					try {
						using (System.IO.StreamWriter logFile = System.IO.File.AppendText(@"c:\GitHub\ActEditor\.cursor\debug.log")) {
							logFile.WriteLine(String.Format("{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"E\",\"location\":\"ApplyColorizeToIndices:340\",\"message\":\"Color transformation applied\",\"data\":{{\"index\":{0},\"originalHue\":{1},\"newHue\":{2},\"hueOffset\":{3},\"originalRgba\":[{4},{5},{6},{7}],\"newRgba\":[{8},{9},{10},{11}]}},\"timestamp\":{12}}}", index, originalHue, newHue, hueOffset, originalColor.R, originalColor.G, originalColor.B, originalColor.A, rgba[0], rgba[1], rgba[2], rgba[3], (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds));
						}
					} catch { }
				}
				// #endregion
			}
			// #region agent log
			try {
				using (System.IO.StreamWriter logFile = System.IO.File.AppendText(@"c:\GitHub\ActEditor\.cursor\debug.log")) {
					logFile.WriteLine(String.Format("{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"E\",\"location\":\"ApplyColorizeToIndices:end\",\"message\":\"Total processed\",\"data\":{{\"totalIndices\":{0},\"processedCount\":{1}}},\"timestamp\":{2}}}", indices.Count, processedCount, (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds));
				}
			} catch { }
			// #endregion
		}

		public GrfColor ConvertToGrayscale(GrfColor originalColor, double tone, GrayscaleType grayscaleType, double contrast, double brightness) {
			// Calculate luminance using standard formula: 0.299*R + 0.587*G + 0.114*B
			double luminance = (0.299 * originalColor.R + 0.587 * originalColor.G + 0.114 * originalColor.B) / 255.0;

			// Apply tone multiplier
			double adjustedLuminance = luminance * tone;

			// Apply contrast: (value - 0.5) * (1 + contrast) + 0.5
			adjustedLuminance = (adjustedLuminance - 0.5) * (1.0 + contrast) + 0.5;

			// Apply brightness
			adjustedLuminance = adjustedLuminance + brightness;

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
