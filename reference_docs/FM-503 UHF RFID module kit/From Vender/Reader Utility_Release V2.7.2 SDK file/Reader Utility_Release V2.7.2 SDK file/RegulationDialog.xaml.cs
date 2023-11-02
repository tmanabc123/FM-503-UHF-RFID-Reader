using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading;
using System.Globalization;
using ReaderServiceDLL;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace ReaderUtility
{
	public partial class RegulationDialog : Window {

		private const int PROC_READ_REGULATION			= 0x01;
		private const int PROC_READ_MODE_AND_CHANNEL	= 0x02;
		private const int PROC_READ_FREQ_OFFSET			= 0x03;
		private const int PROC_READ_POWER				= 0x04;
		private const int PROC_READ_FREQ				= 0x06;
		private const int PROC_SET_FREQ_H				= 0x07;
		private const int PROC_SET_FREQ_L				= 0x08;
		private const int PROC_SET_FREQ					= 0x09;
		private const int PROC_SET_MEASURE_FREQ			= 0X0A;
		private const int PROC_SET_RESET				= 0x0B;
		private const int PROC_FREQ_SET_RESET			= 0x0C;
		private const int PROC_REBOOT					= 0x0D;

		private readonly DispatcherTimer			RepeatEvent = new DispatcherTimer();
		private readonly DispatcherTimer			ProcessEvent = new DispatcherTimer();
		private byte[]								ReceiveData;
		private int									PreProcess;
		private int									mArea = 5;
		private bool								IsSymbol = false;
		private bool								IsAdjust = false, 
													IsPlus = false, 
													IsMinus = false, 
													IsReset = false;
		private bool								IsRunning = false;
		private bool								IsReceiveData = false;
		private bool								IsDateTimeStamp = false;
		private bool								IsSetFrequency = false;
		private ReaderService						ReaderService = null;
		private ReaderService.Module.Version		VersionFW;
		private CultureInfo							Culture;
		private int									mMode;


		public RegulationDialog(ReaderService service, ReaderService.Module.Version v, CultureInfo selected_culture) {
			InitializeComponent();

            this.ReaderService = service;
			this.VersionFW = v;
			this.Culture = selected_culture;

			switch (this.VersionFW) {
				case ReaderService.Module.Version.FI_R3008:
					UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ?
					"The Reader version is not support the advanced setting." :
					"此版本Reader不支援此進階操作", false);
					break;
				case ReaderService.Module.Version.FI_R300A_C1:
				case ReaderService.Module.Version.FI_R300A_C2:
				case ReaderService.Module.Version.FI_R300T_D1:
				case ReaderService.Module.Version.FI_R300T_D2:
				case ReaderService.Module.Version.FI_R300S:
				case ReaderService.Module.Version.FI_RXXXX:
					this.RepeatEvent.Tick += new EventHandler(DoRepeatWork);
					this.RepeatEvent.Interval = TimeSpan.FromMilliseconds(64);
					this.ProcessEvent.Tick += new EventHandler(DoPreProcessWork);
					this.ProcessEvent.Interval = TimeSpan.FromMilliseconds(64);
					this.PreProcess = PROC_READ_REGULATION;
					this.ProcessEvent.Start();
					UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ?
						"get the Reader information." : 
						"取得Reader資訊..", false);

					break;
			}
			ComboBoxSetAreaChanged(this.ComboBoxArea.SelectedIndex);
			ComboBoxSetPowerChanged(this.VersionFW);
			ComboBoxSetStepChanged();

			this.ReaderService.CombineDataReceiveEvent += new ReaderService.CombineDataHandler(DoReceiveDataWork);
		}



		#region #Group Setting
		private void ComboBoxSetAreaChanged(int index) {
			ObservableCollection<string> items = new ObservableCollection<string>();

			switch (index) {
				case 0:
					for (int i = 0; i < 50; i++) {
						double j = 903.24 + i * 0.48;
						items.Add(string.Format("{0}MHz", j.ToString("###.00")));
					}
					items.Add("hopping");
					ComboBoxFrequency.ItemsSource = items;
					ComboBoxMeasureFrequency.ItemsSource = items;
					ComboBoxFrequency.SelectedIndex = 24;
					ComboBoxMeasureFrequency.SelectedIndex = 24;
					break;
				case 1:
					for (int i = 0; i < 13; i++) {
						double j = 922.84 + i * 0.36;
						items.Add(string.Format("{0}MHz", j.ToString("###.00")));
					}
					items.Add("hopping");
					ComboBoxFrequency.ItemsSource = items;
					ComboBoxMeasureFrequency.ItemsSource = items;
					ComboBoxFrequency.SelectedIndex = 6;
					ComboBoxMeasureFrequency.SelectedIndex = 6;
					break;
				case 2:
					for (int i = 0; i < 20; i++) {
						double j = 920.125 + i * 0.25;
						items.Add(string.Format("{0}MHz", j.ToString("###.000")));
					}
					items.Add("hopping");
					ComboBoxFrequency.ItemsSource = items;
					ComboBoxMeasureFrequency.ItemsSource = items;
					ComboBoxFrequency.SelectedIndex = 9;
					ComboBoxMeasureFrequency.SelectedIndex = 9;
					break;
				case 3:
					for (int i = 0; i < 20; i++) {
						double j = 840.125 + i * 0.25;
						items.Add(string.Format("{0}MHz", j.ToString("###.000")));
					}
					items.Add("hopping");
					ComboBoxFrequency.ItemsSource = items;
					ComboBoxMeasureFrequency.ItemsSource = items;
					ComboBoxFrequency.SelectedIndex = 9;
					ComboBoxMeasureFrequency.SelectedIndex = 9;
					break;
				case 4:
					for (int i = 0; i < 4; i++) {
						double j = 865.7 + i * 0.6;
						items.Add(string.Format("{0}MHz", j.ToString("###.00")));
					}
					items.Add("hopping");
					ComboBoxFrequency.ItemsSource = items;
					ComboBoxMeasureFrequency.ItemsSource = items;
					ComboBoxFrequency.SelectedIndex = 1;
					ComboBoxMeasureFrequency.SelectedIndex = 1;
					break;
			}
		}
		private void ComboBoxSetStepChanged() {
			ObservableCollection<string> list = new ObservableCollection<string>();
			foreach (string s in DataRepository.GetStepGroups()) list.Add(s);
			this.mComboboxStep.ItemsSource = list;
			this.mComboboxStep.SelectedIndex = 0;
		}
		private void ComboBoxSetPowerChanged(ReaderService.Module.Version v) {
			ObservableCollection<string> list = new ObservableCollection<string>();
			switch(v){
				case ReaderService.Module.Version.FI_R300T_D1:
				case ReaderService.Module.Version.FI_R300T_D2:
					foreach (PowerItem power in DataRepository.GetPowerGroups(ReaderService.Module.Version.FI_R300T_D1))
					list.Add(string.Format("{0}", power.LocationName));
					break;
				case ReaderService.Module.Version.FI_R300A_C1:
				case ReaderService.Module.Version.FI_R300A_C2:
					foreach (PowerItem power in DataRepository.GetPowerGroups(ReaderService.Module.Version.FI_R300A_C1))
					list.Add(string.Format("{0}", power.LocationName));
					break;
				case ReaderService.Module.Version.FI_R300S:
					foreach (PowerItem power in DataRepository.GetPowerGroups(ReaderService.Module.Version.FI_R300S))
						list.Add(string.Format("{0}", power.LocationName));
					break;
				case ReaderService.Module.Version.FI_RXXXX:
					list.Add("== N/A ==");
					break;
			}
			
			this.mComboboxPower.ItemsSource = list;
			this.mComboboxPower.SelectedIndex = 0;
		}
		private void OnComboBoxSetAreaChanged(object sender, SelectionChangedEventArgs e) {
			if (ComboBoxFrequency != null && ComboBoxMeasureFrequency != null) {
				ComboBoxSetAreaChanged(this.ComboBoxArea.SelectedIndex);
				this.mArea = this.ComboBoxArea.SelectedIndex + 1;
			}
		}
		private void OnComboBoxSetAreaDownClosed(object sender, EventArgs e) {
			if (ComboBoxFrequency != null && ComboBoxMeasureFrequency != null) {
				ComboBoxSetAreaChanged(this.ComboBoxArea.SelectedIndex);

				switch (this.ComboBoxArea.SelectedIndex) {
					case 0: 
						ComboBoxMeasureFrequency.SelectedIndex = 50; 
						OnButtonMeasureSetFrequencyClick(null, null);
						Thread.Sleep(16);
						DoSendWork(this.ReaderService.SetRegulation(ReaderService.Module.Regulation.US), ReaderService.Module.CommandType.Normal); 
						break;
					case 1: 
						ComboBoxMeasureFrequency.SelectedIndex = 13; 
						OnButtonMeasureSetFrequencyClick(null, null);
						Thread.Sleep(16);
						DoSendWork(this.ReaderService.SetRegulation(ReaderService.Module.Regulation.TW), ReaderService.Module.CommandType.Normal); 
						break;
					case 2: 
						ComboBoxMeasureFrequency.SelectedIndex = 20; 
						OnButtonMeasureSetFrequencyClick(null, null);
						Thread.Sleep(16);
						DoSendWork(this.ReaderService.SetRegulation(ReaderService.Module.Regulation.CN), ReaderService.Module.CommandType.Normal); 
						break;
					case 3: 
						ComboBoxMeasureFrequency.SelectedIndex = 20; 
						OnButtonMeasureSetFrequencyClick(null, null);
						Thread.Sleep(16);
						DoSendWork(this.ReaderService.SetRegulation(ReaderService.Module.Regulation.CN2), ReaderService.Module.CommandType.Normal); 
						break;
					case 4: 
						ComboBoxMeasureFrequency.SelectedIndex = 4; 
						OnButtonMeasureSetFrequencyClick(null, null);
						Thread.Sleep(16);
						DoSendWork(this.ReaderService.SetRegulation(ReaderService.Module.Regulation.EU), ReaderService.Module.CommandType.Normal); 
						break;
				}
				DoReceiveWork(ReaderService.Module.CommandType.Normal);
			}

			//this.PreProcess = PROC_SET_MEASURE_FREQ;
			//this.ProcessEvent.Start();
			
		}
		private void OnButtonSetFrequencyClick(object sender, RoutedEventArgs e) {
			int idx = this.ComboBoxFrequency.SelectedIndex + 1;
			string str = this.ReaderService.MakesUpZero(this.ReaderService.ByteToHexString((byte)idx), 2);
			DoSendWork(this.ReaderService.Command_J(0x31, str), ReaderService.Module.CommandType.Normal);
			DoReceiveWork(ReaderService.Module.CommandType.Normal);
		}
		private void OnButtonAdjustClick(object sender, RoutedEventArgs e) {
			if (mTextBoxMeasureFrequency.GetLineText(0) != "") {
				this.IsAdjust = true;
				this.PreProcess = PROC_READ_FREQ;
				this.ProcessEvent.Start();
				UIUnitControl("", false);
			}
			else {
				UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ?
							"enter the measurement frequency.." :
							"請輸入量測頻率", true);
				FocusManager.SetFocusedElement(this, this.mTextBoxMeasureFrequency);
			}
		}
		private void OnButtonSetFrequencyPlusClick(object sender, RoutedEventArgs e) {
			this.IsPlus = true;
			this.PreProcess = PROC_READ_FREQ;
			this.ProcessEvent.Start();
			UIUnitControl("", false);
		}
		private void OnButtonSetFrequencyMinusClick(object sender, RoutedEventArgs e) {
			this.IsMinus = true;
			this.PreProcess = PROC_READ_FREQ;
			this.ProcessEvent.Start();
			UIUnitControl("", false);
		}
		private void OnComboboxStepSelectionChanged(object sender, SelectionChangedEventArgs e) { }
		private void OnButtonSetFrequencyResetClick(object sender, RoutedEventArgs e) {
			this.IsReset = true;
			this.PreProcess = PROC_FREQ_SET_RESET;
			this.ProcessEvent.Start();
			UIUnitControl("", false);
		}
		private void OnComboboxPowerSelectionChanged(object sender, SelectionChangedEventArgs e) { }
		private void OnButtonSetPowerClick(object sender, RoutedEventArgs e) {
			int idx;
			switch (this.VersionFW) {
				case ReaderService.Module.Version.FI_R300S:
				case ReaderService.Module.Version.FI_R300T_D1:
				case ReaderService.Module.Version.FI_R300T_D2:
					idx = 27 - mComboboxPower.SelectedIndex; break;
				case ReaderService.Module.Version.FI_R300A_C1:
				case ReaderService.Module.Version.FI_R300A_C2:
					idx = 20 - mComboboxPower.SelectedIndex; break;
				case ReaderService.Module.Version.FI_RXXXX:
 				default:
					idx = 20 - mComboboxPower.SelectedIndex; break;
			}
			string str = this.ReaderService.MakesUpZero(this.ReaderService.ByteToHexString((byte)idx), 2);
			DoSendWork(this.ReaderService.SetPower(str), ReaderService.Module.CommandType.Normal);
			DoReceiveWork(ReaderService.Module.CommandType.Normal);
		}
		private void OnRadioButtonBasebandModeChecked(object sender, RoutedEventArgs e) {
			var radioButton = sender as RadioButton;
			if (radioButton == null) return;
			this.mMode = Convert.ToInt32(radioButton.Tag.ToString());
		}
		private void OnButtonMeasureSetFrequencyClick(object sender, RoutedEventArgs e) {
			byte[] bs = null;
			string str;
			int idx = ComboBoxMeasureFrequency.SelectedIndex + 1;

			switch (ComboBoxArea.SelectedIndex) {
				case 0:
					if (idx == 51) bs = this.ReaderService.Command_J(0x30, "00");
					break;
				case 1:
					if (idx == 14) bs = this.ReaderService.Command_J(0x30, "00");
					break;
				case 2:
				case 3:
					if (idx == 21) bs = this.ReaderService.Command_J(0x30, "00");
					break;
				
				case 4:
					if (idx == 5) bs = this.ReaderService.Command_J(0x30, "00");
					break;
			}
			if (bs == null) {
				str = this.ReaderService.MakesUpZero(this.ReaderService.ByteToHexString((byte)idx), 2);
				bs = this.ReaderService.Command_J((this.mMode == 2) ? (byte)0x32 : (byte)0x31, str);
			}
			IsSetFrequency = true;
			DoSendWork(bs, ReaderService.Module.CommandType.Normal);
			DoReceiveWork(ReaderService.Module.CommandType.Normal);
		}
		private void OnButtonMeasureRunClick(object sender, RoutedEventArgs e) {
			if (!IsSetFrequency) {
				UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ? "run set frequency first.." : "請先設定頻率..", true);
				ButtonMeasureSetFrequency.Focus();
				return;
			}
			if (!this.IsRunning) {
				this.IsRunning = true;
				this.ButtonMeasureRun.Content = (this.Culture.IetfLanguageTag == "en-US") ? "Stop" : "停止";
				this.RepeatEvent.Start();
				UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ? "running Tag test.." : "正在執行Tag測試..", false);
			}
			else {
				this.IsRunning = false;
				this.ButtonMeasureRun.Content = (this.Culture.IetfLanguageTag == "en-US") ? "Run" : "執行";
			}

		}
		private void OnButtonUpdateClick(object sender, RoutedEventArgs e) {
			this.PreProcess = PROC_READ_REGULATION;
			this.mLabelArea.Text = "";
			this.mLabelFrequncy.Text = "";
			this.mLabelFrequncyOffset.Text = "";
			this.mLabelPower.Text = "";
			this.ProcessEvent.Start();
		}
		private void OnListBoxMenuItemClick_Delete(object sender, RoutedEventArgs e) {
			if (this.mListBox.SelectedIndex == -1) return;
			this.mListBox.Items.Clear();
		}
		#endregion

		private void OnBorderTitleMouseLeftDown(object sender, MouseButtonEventArgs e) {
			this.DragMove();
		}
		private void OnCloseClick(object sender, RoutedEventArgs e) {
			this.Close();
		}
		private void DisplayText(string str, string data) {
			ListBoxItem itm = new ListBoxItem();
			if (str == "TX") {
				this.IsDateTimeStamp = true;
				itm.Foreground = Brushes.SeaGreen;
			}
			else
				itm.Foreground = Brushes.DarkRed;
			if (this.IsDateTimeStamp) {
				if (str == "RX") this.IsDateTimeStamp = false;
				if (data == null)
					itm.Content = string.Format("{0} [{1}] - ", DateTime.Now.ToString("yy/MM/dd H:mm:ss.fff"), str);
				else {
					if (str == "RX")
						itm.Content = string.Format("{0} [{1}] - \n{2}", DateTime.Now.ToString("yy/MM/dd H:mm:ss.fff"), str, this.ReaderService.ShowCRLF(data));
					else
						itm.Content = string.Format("{0} [{1}] - {2}", DateTime.Now.ToString("yy/MM/dd H:mm:ss.fff"), str, this.ReaderService.ShowCRLF(data));
				}
			}
			else {
				if (data == null)
					itm.Content = string.Format("[{0}] - ", str);
				else
					if (IsReceiveData)
						itm.Content = this.ReaderService.ShowCRLF(data);
					else
						itm.Content = string.Format("{0}  -- {1}", this.ReaderService.ShowCRLF(data), DateTime.Now.ToString("H:mm:ss.fff"));
			}

			if (this.mListBox.Items.Count > 1000)
				this.mListBox.Items.Clear();

			this.mListBox.Items.Add(itm);
			this.mListBox.ScrollIntoView(this.mListBox.Items[this.mListBox.Items.Count - 1]);
			itm = null;
		}
		private void UIUnitControl(string s, bool b) {
			this.LabelMessage.Content = s;
			this.mButtonUpdate.IsEnabled = b;
			this.mButtonSetFrequency.IsEnabled = b;
			this.mButtonAdiust.IsEnabled = b;
			this.mButtonSetFrequencyPlus.IsEnabled = b;
			this.mButtonSetFrequencyMinus.IsEnabled = b;
			this.mButtonSetPower.IsEnabled = b;
			this.ButtonMeasureSetFrequency.IsEnabled = b;
			this.ComboBoxArea.IsEnabled = b;
			if (!this.IsRunning)
				this.ButtonMeasureRun.IsEnabled = b;
			this.ButtonSetFrequencyReset.IsEnabled = b;
		}
		private bool IsAlphabetic(string s) {
			Regex r = new Regex(@"^[0-9.]+$");
			return r.IsMatch(s);
		}
		private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) {
			if (!IsAlphabetic(e.Text))
				e.Handled = true;
		}
		private void TextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
			if (e.Key == Key.Space)
				e.Handled = true;
		}

		private void DoRepeatWork(object sender, EventArgs e) {
			if (this.IsRunning) {
				if (!this.IsReceiveData) {
					this.IsReceiveData = true;
					DoSendWork(this.ReaderService.Command_U(), ReaderService.Module.CommandType.Normal);
				}
			}
			else {
				while (this.IsReceiveData) { Thread.Sleep(32); }
				this.RepeatEvent.Stop();
				UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ? "Tag test is stop." : "已停止Tag測試", true);
			}
			
		}
		private void DoSendWork(byte[] bs, ReaderService.Module.CommandType t) {
			if (bs != null) {
				if (t == ReaderService.Module.CommandType.Normal)
					DisplayText("TX", this.ReaderService.ShowCRLF(this.ReaderService.BytesToString(bs)));			
				else
					DisplayText("TX", this.ReaderService.BytesToHexString(bs));			
				this.ReaderService.Send(bs, t);
			}
		}
		private byte[] DoReceiveWork(ReaderService.Module.CommandType t) {
			byte[] b = this.ReaderService.Receive();
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(()=>{
				ListBoxItem itm = new ListBoxItem();
				itm.Foreground = Brushes.DarkRed;

				if (t == ReaderService.Module.CommandType.Normal)
					itm.Content = string.Format("{0} [RX] - {1}", DateTime.Now.ToString("yy/MM/dd H:mm:ss.fff"), this.ReaderService.ShowCRLF(this.ReaderService.BytesToString(b)));
				else
					itm.Content = string.Format("{0} [RX] - {1}", DateTime.Now.ToString("yy/MM/dd H:mm:ss.fff"), this.ReaderService.BytesToHexString(b));
				this.mListBox.Items.Add(itm);
				this.mListBox.ScrollIntoView(this.mListBox.Items[this.mListBox.Items.Count - 1]);
			}));
			
			return b;
		}
		private void DoPreProcessWork(object sender, EventArgs e) {
			byte[] bs = null;
			string str0;
			switch (this.PreProcess) {
				case PROC_READ_REGULATION:
					UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ? "get frequency information." : "讀取頻率資訊..", false);
					DoSendWork(this.ReaderService.ReadRegulation(), ReaderService.Module.CommandType.Normal);
					this.ReceiveData = DoReceiveWork(ReaderService.Module.CommandType.Normal);

					if (this.ReceiveData == null) {
						UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ?
							"the Reader has no callback message, check and confirm the connecting status, please." : 
							"沒有任何回覆，請確認Reader是否連上", 
							false);
						this.ProcessEvent.Stop();
					}
					else {
						if (this.ReceiveData[1] == 0x4E) {
							switch (this.ReceiveData[3]) {
								case 0x31: this.mLabelArea.Text = "01: US 902~928"; mArea = 1; break;
								case 0x32: this.mLabelArea.Text = "02: TW 922~928"; mArea = 2; break;
								case 0x33: this.mLabelArea.Text = "03: CN 920~925"; mArea = 3; break;
								case 0x34: this.mLabelArea.Text = "04: CN2 840~845"; mArea = 4; break;
								case 0x35: this.mLabelArea.Text = "05: EU 865~868"; mArea = 5; break;
							}
							this.ComboBoxArea.SelectedIndex = mArea - 1;
							this.PreProcess = PROC_READ_MODE_AND_CHANNEL;
						}
						else {
							UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ? "error callback." : "錯誤回覆",  true);
							this.ProcessEvent.Stop();
						}
					}
					break;
				case PROC_READ_MODE_AND_CHANNEL:
					UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ? "get the regulation mode and channel." : "讀取頻率模式和頻道..", false);
					switch (this.VersionFW) {
						case ReaderService.Module.Version.FI_R300A_C1:
						case ReaderService.Module.Version.FI_R300T_D1:
							DoSendWork(this.ReaderService.Command_AA("FF04008702"), ReaderService.Module.CommandType.AA);
							this.ReceiveData = DoReceiveWork(ReaderService.Module.CommandType.AA);	
							break;
						case ReaderService.Module.Version.FI_R300A_C2:
						case ReaderService.Module.Version.FI_R300T_D2:
						case ReaderService.Module.Version.FI_R300S:
							DoSendWork(this.ReaderService.ReadModeandChannel(), ReaderService.Module.CommandType.Normal);
							bs = DoReceiveWork(ReaderService.Module.CommandType.Normal);
							this.ReceiveData = this.ReaderService.HexStringToBytes(this.ReaderService.RemoveCRLF(this.ReaderService.BytesToString(bs)));
							break;
					}
					if (this.ReceiveData[0] == 0xFF || this.ReceiveData[0] == 0x0) this.mLabelFrequncy.Text = "hopping";
					else {
						string s = null, m;
						double j;
						int i = (this.ReceiveData[1] > 0) ? this.ReceiveData[1] - 1 : this.ReceiveData[1];
						switch (mArea) {
							case 1:
								j = 903.24 + i * 0.48;
								s = string.Format("{0}MHz", j.ToString("###.00"));
								break;
							case 2:
								j = 922.84 + i * 0.36;
								s = string.Format("{0}MHz", j.ToString("###.00"));
								break;
							case 3:
								j = 920.125 + i * 0.25;
								s = string.Format("{0}MHz", j.ToString("###.000"));
								break;
							case 4:
								j = 840.125 + i * 0.25;
								s = string.Format("{0}MHz", j.ToString("###.000"));
								break;
							case 5:
								j = 865.7 + i * 0.6;
								s = string.Format("{0}MHz", j.ToString("###.00"));
								break;
						}

						if (this.ReceiveData[0] == 0x01) {
							m = "Carry";
							mBasebandCarryMode.IsChecked = true;
						}
						else {
							m = "RX";
							mBasebandRXMode.IsChecked = true;
						}
						this.mLabelFrequncy.Text = string.Format("Fix mode, {0} Freq. = {1}", m, s);
					}
						
					this.PreProcess = PROC_READ_FREQ_OFFSET;
					break;
				case PROC_READ_FREQ_OFFSET:
					UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ? "get the Reader frequency offset.." : "讀取Reader頻率Offset..", false);
					switch (this.VersionFW) {
						case ReaderService.Module.Version.FI_R300A_C1:
						case ReaderService.Module.Version.FI_R300T_D1:
							DoSendWork(this.ReaderService.Command_AA("FF04008903"), ReaderService.Module.CommandType.AA);
							this.ReceiveData = DoReceiveWork(ReaderService.Module.CommandType.AA);
							break;
						case ReaderService.Module.Version.FI_R300A_C2:
						case ReaderService.Module.Version.FI_R300T_D2:
						case ReaderService.Module.Version.FI_R300S:
							DoSendWork(this.ReaderService.ReadFrequencyOffset(), ReaderService.Module.CommandType.Normal);
							bs = DoReceiveWork(ReaderService.Module.CommandType.Normal);
							this.ReceiveData = this.ReaderService.HexStringToBytes(this.ReaderService.RemoveCRLF(this.ReaderService.BytesToString(bs)));
							break;
					}

					if (this.ReceiveData[0] > 0x01)
						this.mLabelFrequncyOffset.Text = "N/A";
					else {
						string strSymbol = (ReceiveData[0] == 0x00) ? "-" : "+";
						int ii = ((ReceiveData[1] << 8) & 0xFF00) + (ReceiveData[2] & 0xFF);
						double db = (double)ii * (double)30.5;
						mLabelFrequncyOffset.Text = string.Format("{0}{1}Hz", strSymbol, db.ToString());
						
					}	
					this.PreProcess = PROC_READ_POWER;
					break;
				case PROC_READ_POWER:
					UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ? "get the Reader power.." : "讀取Reader功率..", false);
					DoSendWork(this.ReaderService.ReadPower(), ReaderService.Module.CommandType.Normal);
					this.ReceiveData = DoReceiveWork(ReaderService.Module.CommandType.Normal);
					if (ReceiveData[0] == 0xFF) 
						this.mLabelPower.Text = "N/A";
					else {
						if (ReceiveData[1] == 0x4E) {
							byte[] b = new byte[] { ReceiveData[2], ReceiveData[3] };
							string str = System.Text.Encoding.ASCII.GetString(b);

							switch (this.VersionFW) {
								case ReaderService.Module.Version.FI_R300T_D1:
								case ReaderService.Module.Version.FI_R300T_D2:
									if (this.ReaderService.HexStringToByte(str) >= 0x1B) {
										this.mLabelPower.Text = "25dBm";
										this.mComboboxPower.SelectedIndex = 0;
									}
									else {
										this.mLabelPower.Text = string.Format("{0}dBm", this.ReaderService.HexStringToByte(str) - 2);
										this.mComboboxPower.SelectedIndex = 25 - (this.ReaderService.HexStringToByte(str) - 2);
									}
									break;
								case ReaderService.Module.Version.FI_R300A_C1:
								case ReaderService.Module.Version.FI_R300A_C2:
									if (this.ReaderService.HexStringToByte(str) >= 0x14) {
										this.mLabelPower.Text = "18dBm";
										this.mComboboxPower.SelectedIndex = 0;
									}
									else {
										this.mLabelPower.Text = string.Format("{0}dBm", this.ReaderService.HexStringToByte(str) - 2);
										this.mComboboxPower.SelectedIndex = 18 - (this.ReaderService.HexStringToByte(str) - 2);
									}
									break;
								case ReaderService.Module.Version.FI_R300S:
									if (this.ReaderService.HexStringToByte(str) >= 0x1B) {
										this.mLabelPower.Text = "27dBm";
										this.mComboboxPower.SelectedIndex = 0;
									}
									else {
										this.mLabelPower.Text = string.Format("{0}dBm", this.ReaderService.HexStringToByte(str));
										this.mComboboxPower.SelectedIndex = 27 - this.ReaderService.HexStringToByte(str);
									}
									break;
								case ReaderService.Module.Version.FI_RXXXX:
									this.mLabelPower.Text = "N/A";
									break;
							}
						}
					}

					this.ProcessEvent.Stop();
					UIUnitControl("", true);
					break;

				case PROC_READ_FREQ:
				case PROC_FREQ_SET_RESET:
					if (!IsReset) {
						str0 = null;
						double d0 = 0.0;
						switch (this.VersionFW) {
							case ReaderService.Module.Version.FI_R300A_C1:
							case ReaderService.Module.Version.FI_R300T_D1:
								DoSendWork(this.ReaderService.Command_AA("FF04008903"), ReaderService.Module.CommandType.AA);
								this.ReceiveData = DoReceiveWork(ReaderService.Module.CommandType.AA);
								break;
							case ReaderService.Module.Version.FI_R300A_C2:
							case ReaderService.Module.Version.FI_R300T_D2:
							case ReaderService.Module.Version.FI_R300S:
								DoSendWork(this.ReaderService.ReadFrequencyOffset(), ReaderService.Module.CommandType.Normal);
								bs = DoReceiveWork(ReaderService.Module.CommandType.Normal);
								this.ReceiveData = this.ReaderService.HexStringToBytes(this.ReaderService.RemoveCRLF(this.ReaderService.BytesToString(bs)));
								break;
						}
						if (ReceiveData == null) {
							UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ?
								"the Reader has no callback message, check and confirm the connecting status, please." :
								"沒有任何回覆，確認Reader是否連上",
								false);
							this.ProcessEvent.Stop();
							return;
						}

						if (IsPlus | IsMinus | IsAdjust) {
							str0 = (ReceiveData[0] > 0x01) ? "{N/A}" : (ReceiveData[0] == 0x01) ? "+" : "-";
							if (ReceiveData[0] == 0xFF) {
								if (ReceiveData[1] == 0xFF) ReceiveData[1] = 0;
								if (ReceiveData[2] == 0xFF) ReceiveData[2] = 0;
							}
							d0 = ((ReceiveData[1] << 8) + ReceiveData[2]) * 30.5;
						}

						if (IsPlus) {
							int value = int.Parse(mComboboxStep.SelectedValue.ToString());
							int b = (int)((ReceiveData[1] << 8) + ReceiveData[2]);
							if (ReceiveData[0] > 0x0) { value += b; ReceiveData[0] = 0x1; }
							else {
								if (value > b) { value = value - b; ReceiveData[0] = 0x1; }
								else value = b - value;
							}
							ReceiveData[1] = (byte)(value >> 8);
							ReceiveData[2] = (byte)(value & 0xFF);

							string str1 = (ReceiveData[0] > 0) ? "+" : "-";
							double d1 = ((ReceiveData[1] << 8) + ReceiveData[2]) * 30.5;
							UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ?
								string.Format("offset freq. {0}{1}Hz，adjust to {2}{3}Hz，and waiting for reboot.", str0, d0, str1, d1) :
								string.Format("目前offset頻率 {0}{1}Hz，調整為 {2}{3}Hz，並等候Reader重啟", str0, d0, str1, d1), false);
						}
						else if (IsMinus) {
							int value = int.Parse(mComboboxStep.SelectedValue.ToString());
							int b = (int)((ReceiveData[1] << 8) + ReceiveData[2]);
							if (ReceiveData[0] > 0x0) {
								if (value > b) { value = value - b; ReceiveData[0] = 0x0; }
								else value = b - value; }
							else { value += b; ReceiveData[0] = 0x0; }
							ReceiveData[1] = (byte)(value >> 8);
							ReceiveData[2] = (byte)(value & 0xFF);

							string str2 = (ReceiveData[0] > 0) ? "+" : "-";
							double d2 = ((ReceiveData[1] << 8) + ReceiveData[2]) * 30.5;
							UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ?
								string.Format("offset freq. {0}{1}Hz，adjust to {2}{3}Hz，and waiting for reboot.", str0, d0, str2, d2) :
								string.Format("目前offset頻率 {0}{1}Hz，調整為 {2}{3}Hz，並等候Reader重啟", str0, d0, str2, d2), false);
						}
						else if (IsAdjust) {
							double fc, fm, fb, tf = 0;
							if (ReceiveData[0] == 0x00)//-
								fm = double.Parse(mTextBoxMeasureFrequency.GetLineText(0)) * 1000000 + ((ReceiveData[1] << 8) + ReceiveData[2]) * 30.5;
							else
								fm = double.Parse(mTextBoxMeasureFrequency.GetLineText(0)) * 1000000 - ((ReceiveData[1] << 8) + ReceiveData[2]) * 30.5;
							int idx = ComboBoxFrequency.SelectedIndex;
							switch (mArea) {
								case 1: tf = 903.24 + idx * 0.48; break;
								case 2: tf = 922.84 + idx * 0.36; break;
								case 3: tf = 920.125 + idx * 0.25; break;
								case 4: tf = 840.125 + idx * 0.25; break;
								case 5: tf = 865.7 + idx * 0.6; break;
							}
							fc = tf * 1000000;
							fb = fc - fm;
							if (fb <= 0) {
								fb = fm - fc;
								IsSymbol = false;
							}
							else
								IsSymbol = true;

							ReceiveData[1] = (byte)(((int)(fb / 30.5) >> 8) & 0xFF);
							ReceiveData[2] = (byte)((int)(fb / 30.5) & 0xFF);
							//this.PreProcess = PROC_READ_FREQ_CALLBACK;
							//this.ProcessEvent.Start();
							UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ?
								string.Format("adjust freq. to {0}Hz，and waiting for reboot.", fm) :
								string.Format("修正頻率至{0}, 並等待Reader重啟", fm),
								false);
						}
					}
					else {
						UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ? "reset offset frequency，and waiting for reboot." : "重置offset頻率，並等候Reader重啟", false);
					}
					this.PreProcess = PROC_SET_FREQ_H;
					break;
				case PROC_SET_FREQ_H:
					switch (this.VersionFW) {
						case ReaderService.Module.Version.FI_R300A_C1:
						case ReaderService.Module.Version.FI_R300T_D1:
							str0 = string.Format("FF05008A{0}", (IsReset) ? "FF" : this.ReaderService.ByteToHexString(ReceiveData[1]));
							DoSendWork(this.ReaderService.Command_AA(str0), ReaderService.Module.CommandType.AA);
							DoReceiveWork(ReaderService.Module.CommandType.AA);
							break;
						case ReaderService.Module.Version.FI_R300A_C2:
						case ReaderService.Module.Version.FI_R300T_D2:
						case ReaderService.Module.Version.FI_R300S:
							str0 = (IsReset) ? "FF" : this.ReaderService.ByteToHexString(ReceiveData[1]);
							DoSendWork(this.ReaderService.SetFrequencyAddrH(str0), ReaderService.Module.CommandType.Normal);
							DoReceiveWork(ReaderService.Module.CommandType.Normal);
							break;
					}			
					this.PreProcess = PROC_SET_FREQ_L;
					break;
				case PROC_SET_FREQ_L:
					switch (this.VersionFW) {
						case ReaderService.Module.Version.FI_R300A_C1:
						case ReaderService.Module.Version.FI_R300T_D1:
							str0 = string.Format("FF05008B{0}", (IsReset) ? "FF" : this.ReaderService.ByteToHexString(ReceiveData[2]));
							DoSendWork(this.ReaderService.Command_AA(str0), ReaderService.Module.CommandType.AA);
							DoReceiveWork(ReaderService.Module.CommandType.AA);
							break;
						case ReaderService.Module.Version.FI_R300A_C2:
						case ReaderService.Module.Version.FI_R300T_D2:
						case ReaderService.Module.Version.FI_R300S:
							str0 = (IsReset) ? "FF" : this.ReaderService.ByteToHexString(ReceiveData[2]);
							DoSendWork(this.ReaderService.SetFrequencyAddrL(str0), ReaderService.Module.CommandType.Normal);
							DoReceiveWork(ReaderService.Module.CommandType.Normal);
							break;
					}			
					
					this.PreProcess = PROC_SET_FREQ;
					break;
				case PROC_SET_FREQ:
					switch (this.VersionFW) {
						case ReaderService.Module.Version.FI_R300A_C1:
						case ReaderService.Module.Version.FI_R300T_D1:
							str0 = string.Format("FF060089{0}", (IsReset) ? "FF" :
															(IsPlus | IsMinus) ? this.ReaderService.ByteToHexString(ReceiveData[0]) :
																IsSymbol ? "01" : "00");	
							this.IsPlus = false;
							this.IsMinus = false;
							this.IsReset = false;
							if (this.IsAdjust) {
								this.mTextBoxMeasureFrequency.Text = String.Empty;
								this.IsAdjust = false;
							}
							DoSendWork(this.ReaderService.Command_AA(str0), ReaderService.Module.CommandType.AA);
							DoReceiveWork(ReaderService.Module.CommandType.AA);
							break;
						case ReaderService.Module.Version.FI_R300A_C2:
						case ReaderService.Module.Version.FI_R300T_D2:
						case ReaderService.Module.Version.FI_R300S:
							str0 = (IsReset) ? "FF" : (IsPlus | IsMinus) ? this.ReaderService.ByteToHexString(ReceiveData[0]) : IsSymbol ? "01" : "00";
							this.IsPlus = false;
							this.IsMinus = false;
							this.IsReset = false;
							if (this.IsAdjust) {
								this.mTextBoxMeasureFrequency.Text = String.Empty;
								this.IsAdjust = false;
							}
							DoSendWork(this.ReaderService.SetFrequency(str0), ReaderService.Module.CommandType.Normal);
							DoReceiveWork(ReaderService.Module.CommandType.Normal);
							break;
					}		
					this.PreProcess = PROC_SET_RESET;
					break;
				case PROC_SET_MEASURE_FREQ:
					Thread.Sleep(1000);
					OnButtonMeasureSetFrequencyClick(null, null);
					this.ProcessEvent.Stop();
					break;
				case PROC_SET_RESET:
					Thread.Sleep(2000);
					DoReceiveWork(ReaderService.Module.CommandType.Normal);					
					UIUnitControl((this.Culture.IetfLanguageTag == "en-US") ?
						"the Reader has reboot." : 
						"Reader已重啟完成", 
						true);
					this.ProcessEvent.Stop();
					break;
			}
		}
		private void DoReceiveDataWork(object sender, ReaderService.CombineDataReceiveArgument e) {
			string s_crlf = e.Data;
			if (s_crlf.Equals("\nU\r\n")) this.IsReceiveData = false;
			if (this.IsRunning) {	
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
					DisplayText("RX", s_crlf);
				}));
			}
			
		}
	}
}
