using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.ComponentModel;

namespace ReaderUtility
{
	public class CulturesHelper
	{
		private static bool _isFoundInstalledCultures = false;

		private static List<CultureInfo> _supportedCultures = new List<CultureInfo>();

		private static ObjectDataProvider _objectDataProvider;

		private static CultureInfo _designTimeCulture = new CultureInfo("zh-TW");

		public static List<CultureInfo> SupportedCultures {
			get {
				return _supportedCultures;
			}
		}

		public CulturesHelper() {
			if (!_isFoundInstalledCultures) {

				CultureInfo cultureInfo = new CultureInfo("");

				foreach (string dir in Directory.GetDirectories(System.Windows.Forms.Application.StartupPath)) {
					try {
						DirectoryInfo dirinfo = new DirectoryInfo(dir);
						cultureInfo = CultureInfo.GetCultureInfo(dirinfo.Name);

						if (dirinfo.GetFiles(Path.GetFileNameWithoutExtension(
							System.Windows.Forms.Application.ExecutablePath) + ".resources.dll").Length > 0) {
							_supportedCultures.Add(cultureInfo);
						}
					}
					catch (Exception e) { }
				}

				if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) {
					Properties.Resources.Culture = _designTimeCulture;
					Properties.Settings.Default.DefaultCulture = _designTimeCulture;
				}
				else if (_supportedCultures.Count > 0 && Properties.Settings.Default.DefaultCulture != null) {
					Properties.Resources.Culture = Properties.Settings.Default.DefaultCulture;
				}

				_isFoundInstalledCultures = true;
			}
		}

		public Properties.Resources GetResourceInstance() {
			return new Properties.Resources();
		}

		public Properties.Resources GetResourceInstance(string cultureName) {
			ChangeCulture(new CultureInfo(cultureName));

			return new Properties.Resources();
		}


		public static ObjectDataProvider ResourceProvider {
			get {
				if (_objectDataProvider == null) {
					_objectDataProvider = (ObjectDataProvider)App.Current.FindResource("Resources");
				}
				return _objectDataProvider;
			}
		}

		public static void ChangeCulture(CultureInfo culture) {
			if (_supportedCultures.Contains(culture)) {
				Properties.Resources.Culture = culture;
				Properties.Settings.Default.DefaultCulture = culture;
				Properties.Settings.Default.Save();

				ResourceProvider.Refresh();
			}
		}
	}
}
