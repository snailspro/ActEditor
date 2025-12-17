using System;
using System.Windows;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using ActEditor.Tools.PaletteGenerator;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF;

namespace Scripts {
	public class Script : IActScript {
		public object DisplayName {
			get { return "Palette Generator..."; }
		}

		public string Group {
			get { return "Scripts"; }
		}

		public string InputGesture {
			get { return "{Scripts.PaletteGenerator}"; }
		}

		public string Image {
			get { return "pal.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null || act.Sprite == null || act.Sprite.Palette == null) {
				ErrorHandler.HandleException("O ACT atual não possui paleta. Abra um ACT com paleta primeiro.", ErrorLevel.Warning);
				return;
			}

			var dialog = new PaletteGeneratorWindow(act);
			dialog.Owner = WpfUtilities.TopWindow;
			dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			dialog.Closed += delegate {
				if (dialog.Owner != null)
					dialog.Owner.Focus();
			};
			dialog.ShowDialog();
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && act.Sprite != null && act.Sprite.Palette != null;
		}
	}
}
