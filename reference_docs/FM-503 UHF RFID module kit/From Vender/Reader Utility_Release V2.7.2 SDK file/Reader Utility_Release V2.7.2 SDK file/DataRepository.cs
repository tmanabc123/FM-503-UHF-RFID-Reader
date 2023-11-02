using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows;
using System.Collections.ObjectModel;
using ReaderServiceDLL;

namespace ReaderUtility {

	public class PowerItem {
		public string LocationName { get; set; }
		public string LocationValue { get; set; }
	}

	public class DataRepository {
		public static List<PowerItem> GetPowerGroups(ReaderService.Module.Version v){
			var __list = new List<PowerItem>();
			int i;

			switch (v) {
				case ReaderService.Module.Version.FI_R300T_D1:
				case ReaderService.Module.Version.FI_R300T_D2:
					for (i = 27; i >= 0; i-- )
						__list.Add(new PowerItem() { LocationName = string.Format("{0} dBm", i-2), LocationValue = i.ToString("X2")});
					break;
				default:
				case ReaderService.Module.Version.FI_R300A_C1:
				case ReaderService.Module.Version.FI_R300A_C2:
					for (i = 20; i >= 0; i--)
						__list.Add(new PowerItem() { LocationName = string.Format("{0} dBm", i - 2), LocationValue = i.ToString("X2") });
					break;
				case ReaderService.Module.Version.FI_R300S:
					for (i = 27; i >= 0; i--)
						__list.Add(new PowerItem() { LocationName = string.Format("{0} dBm", i), LocationValue = i.ToString("X2") });
					break;
			}	
			return __list;
		}

		public static List<string> GetStepGroups() {
			var __list = new List<string>();

			__list.Add("1");
			__list.Add("2");
			__list.Add("3");
			__list.Add("4");
			__list.Add("5");
			__list.Add("6");
			__list.Add("7");
			__list.Add("8");
			__list.Add("9");
			__list.Add("10");
			__list.Add("20");
			__list.Add("50");
			__list.Add("100");
			return __list;
		}
	}
}
