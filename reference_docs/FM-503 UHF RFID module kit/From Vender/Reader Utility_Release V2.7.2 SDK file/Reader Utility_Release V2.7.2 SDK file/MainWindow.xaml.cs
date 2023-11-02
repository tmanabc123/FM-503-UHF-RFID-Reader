using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using System.Threading;
using System.Collections;
using System.IO.Ports;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using ReaderServiceDLL;
using System.Runtime.InteropServices;
using System.ComponentModel;


namespace ReaderUtility
{
	public partial class MainWindow : Window {
		enum ValidationStates					{ OK, ERROR, WARNING, FOCUS };
		enum CommandStates						{ DEFAULT, INFO, EPC, TID, SELECT, PASSWORD, MULTI, READ, WRITE, LOCK, KILL, B02_MULTIREAD, B02_MULTIREAD_Q, B02_MULTI, B02_MULTI_Q };
		enum GroupStatus						{ ALL, EPC_DATA, READ_WRITE, TAG_SELECT, TAG_LOCK, TAG_KILL, READER_SET, TAG_RECORD, TAG_MSG, MULTI_CHECK, EPC_REPEAT, TID_REPEAT, BTN_CLR, BTN_SAVE, BTN_TID, BTN_EPC, BTN_EPCU, CB_CULTURE,
												B02PREVIOUS, B02NEXT, B02BTN_CLR, B02BTN_SAVE, B02Q, B02CONTINUE, B02READ };
		private CommandStates					DoProcess;
		private ReaderService					ReaderService = null;
		private ReaderService.Module.Version	VersionFW;
		private ConnectDialog					ConnectDialog = null;
		private RegulationDialog				RegulationDialog_ = null;
		private SerialPort						SerialPort;
		private Thread							InfoThread;
		private readonly DispatcherTimer		RepeatEvent = new DispatcherTimer();
		private Hashtable						Regex = new Hashtable();
		private Hashtable						ValidationState = new Hashtable();
		private CultureInfo						Culture;
		private List<UIControl>					UIControPackets = new List<UIControl>();
		private List<UIControl>					UITempControPackets = new List<UIControl>();
		private bool							IsRepeat = false;
		private bool							IsB02Repeat = false;
		private bool							IsMenuLockGroup = false, IsMenuTagCount = false;
		private bool							IsReceiveDataWork = false;
		private bool							IsDateTimeStamp = false;
		private bool							IsFocus = false;
		
		//private string							SubPackets = null;
		//private string							Packets = null;
		private int								Mask_, Action_;
		private int								TagCount_ = 0, B02TagCount_ = 0, RunCount_ = 0, B02RunCount_ = 0;
		

		class MemBankEPC {
			private const int length_ = 4;
			public int Length { get { return length_; } }
			public string _PC { get; set; }
			public string _EPC { get; set; }
			public string _CRC16 { get; set; }
			public string _Count { get; set; }
			public string _Percentage { get; set; }
		}
		class B02MemBankEPC {
			private const int length_ = 4;
			public int Length { get { return length_; } }
			public string _B02PC { get; set; }
			public string _B02EPC { get; set; }
			public string _B02CRC16 { get; set; }
			public string _B02Read { get; set; }
			public string _B02Count { get; set; }
			public string _B02Percentage { get; set; }
		}
		class UIControl {
			public GroupStatus Group { get; set; }
			public bool Status { get; set; }

			public UIControl(GroupStatus g, bool s) { Group = g; Status = s; }
		}

		public MainWindow() {
			InitializeComponent();

			this.Regex["BitAddress"] = @"[0-9a-fA-F]";
			this.ReaderService = new ReaderService();
			this.ConnectDialog = new ConnectDialog();
			this.ConnectDialog.ShowDialog();
			if (ConnectDialog.DialogResult.HasValue && !ConnectDialog.DialogResult.Value) this.Close();
			else if (ConnectDialog.DialogResult.HasValue && ConnectDialog.DialogResult.Value) {
				this.ReaderService = this.ConnectDialog.GetService();
				this.SerialPort = this.ReaderService.GetSerialPort();
				this.ReaderService.CombineDataReceiveEvent += new ReaderService.CombineDataHandler(DoReceiveDataWork);
				this.ReaderService.RawDataReceiveEvent += new ReaderService.RawDataHandler(DoRawReceiveDataWork);
				this.CheckBoxStatus.Tag = "True";
				this.TextBlockStatus.Text = string.Format("{0} ({1},{2},{3},{4})",
															this.SerialPort.PortName,
															this.SerialPort.BaudRate,
															this.SerialPort.DataBits,
															this.SerialPort.Parity,
															this.SerialPort.StopBits);
				this.RepeatEvent.Tick += new EventHandler(DoRepeatWork);
				this.RepeatEvent.Interval = TimeSpan.FromMilliseconds(32);
				this.ComboBoxCulture.SelectedIndex = 0;

				this.B01ButtonReadEPC.Visibility = Visibility.Visible;
				this.B01ButtonReadEPC_U.Visibility = Visibility.Collapsed;
				this.B01ButtonReadEPC.Content = (this.Culture.IetfLanguageTag == "en-US") ? "EPC (Q)" : "讀取EPC(Q)";

				this.B01TextBoxMask.Visibility = Visibility.Collapsed;
				this.B01TextBoxAction.Visibility = Visibility.Collapsed;

				this.InfoThread = new Thread(DoInfoWork);
				this.InfoThread.IsBackground = true;
				this.InfoThread.Start();

				((INotifyCollectionChanged)B02ListBox.Items).CollectionChanged += B02ListBoxCollectionChanged;
			}
		}

		private void GroupStatusControl(GroupStatus g, bool b) {
			switch (g) {
				case GroupStatus.TAG_SELECT: this.B01GroupSelect.IsEnabled = b; break;
				case GroupStatus.EPC_DATA: this.B01GroupEPC.IsEnabled = b; break;
				case GroupStatus.READ_WRITE: this.B01GroupReadWrite.IsEnabled = b; break;
				case GroupStatus.TAG_LOCK: this.B01GroupLock.IsEnabled = b; break;
				case GroupStatus.TAG_KILL: this.B01GroupKill.IsEnabled = b; break;
				case GroupStatus.READER_SET: this.B01GroupSetting.IsEnabled = b; break;
				case GroupStatus.TAG_RECORD: this.B01GroupRecord.IsEnabled = b; break;
				case GroupStatus.TAG_MSG: this.B01GroupMsg.IsEnabled = b; break;
				case GroupStatus.MULTI_CHECK: this.B01CheckBoxMulti.IsEnabled = b; break;
				case GroupStatus.EPC_REPEAT: this.B01CheckBoxRepeat.IsEnabled = b; break;
				case GroupStatus.TID_REPEAT: this.B01CheckBoxRepeatTID.IsEnabled = b; break;
				case GroupStatus.BTN_CLR: this.B01ButtonClear.IsEnabled = b; break;
				case GroupStatus.BTN_SAVE: this.B01ButtonSave.IsEnabled = b; break;
				case GroupStatus.CB_CULTURE: this.ComboBoxCulture.IsEnabled = b; break;
				case GroupStatus.BTN_TID: this.B01ButtonTID.IsEnabled = b; break;
				case GroupStatus.BTN_EPC: this.B01ButtonReadEPC.IsEnabled = b; break;
				case GroupStatus.BTN_EPCU: this.B01ButtonReadEPC_U.IsEnabled = b; break;
				case GroupStatus.B02BTN_CLR: this.B02ButtonClear.IsEnabled = b; break;
				case GroupStatus.B02BTN_SAVE: this.B02ButtonSave.IsEnabled = b; break;
				case GroupStatus.B02PREVIOUS: this.PreviousButton.IsEnabled = b; break;
				case GroupStatus.B02NEXT: this.NextButton.IsEnabled = b; break;
				case GroupStatus.B02CONTINUE: this.B02Repeat.IsEnabled = b; break;
				case GroupStatus.B02Q: this.B02QSetting.IsEnabled = b; break;
				case GroupStatus.B02READ: this.B02GroupControl.IsEnabled = b; break;
				case GroupStatus.ALL:
					this.B01GroupSelect.IsEnabled = b;
					this.B01GroupEPC.IsEnabled = b;
					this.B01GroupReadWrite.IsEnabled = b;
					this.B01GroupLock.IsEnabled = b;
					this.B01GroupKill.IsEnabled = b;
					this.B01GroupSetting.IsEnabled = b;
					this.B01GroupRecord.IsEnabled = b;
					this.B01GroupMsg.IsEnabled = b;
					this.B01CheckBoxMulti.IsEnabled = b;
					this.B01CheckBoxRepeat.IsEnabled = b;
					this.B01CheckBoxRepeatTID.IsEnabled = b;
					this.B01ButtonClear.IsEnabled = b;
					this.B01ButtonSave.IsEnabled = b;
					this.ComboBoxCulture.IsEnabled = b;
					this.B01ButtonTID.IsEnabled = b;
					this.B01ButtonReadEPC.IsEnabled = b;
					this.B01ButtonReadEPC_U.IsEnabled = b;
					this.B02ButtonClear.IsEnabled = b;
					this.B02ButtonSave.IsEnabled = b;
					this.B02Repeat.IsEnabled = b;
					this.B02QSetting.IsEnabled = b;
					this.B02GroupControl.IsEnabled = b;
					this.B02ButtonReadUR.IsEnabled = b;
					break;
			}
		}
		private void UIControlStatus(List<UIControl> list, bool b) {
			for (int i = 0; i < list.Count; i++) {
				if (b) GroupStatusControl(list[i].Group, list[i].Status);
				else GroupStatusControl(list[i].Group, !list[i].Status);
			}
		}
		private void B02ListBoxCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
			if (e.Action == NotifyCollectionChangedAction.Add) {
				B02ListBox.ScrollIntoView(e.NewItems[0]);
			}
		}

		[DllImport("psapi.dll")]
		static extern int EmptyWorkingSet(IntPtr hwProc);
		public void ClearMemory() {
			Process process;
			process = Process.GetCurrentProcess();
			try {
				EmptyWorkingSet(process.Handle);
			}
			catch { }
		}
		private void Window_Loaded(object sender, RoutedEventArgs e) {
			this.MinWidth = this.ActualWidth;
			this.MinHeight = this.ActualHeight;
			this.MaxHeight = this.ActualHeight;
		}
		private void OnBorderTitleMouseLeftDown(object sender, MouseButtonEventArgs e) { this.DragMove(); }
		private void OnButtonCloseClick(object sender, RoutedEventArgs e) {
			if (this.ReaderService.IsOpen()) this.ReaderService.Close();
			this.Close(); 
		}

		private void DoRawReceiveDataWork(object sender, ReaderService.RawDataReceiveArgument e) {
			//string str = this.ReaderService.BytesToString(e.Data);
			//str = this.ReaderService.ShowCRLF(str);
			
			//DisplayInfoMsg("RX", str, "<LF>", "\n<LF>");
		}
		private void DoReceiveDataWork(object sender, ReaderService.CombineDataReceiveArgument e) {
			string s_crlf = e.Data;
			switch (DoProcess) {
				case CommandStates.EPC:
					if (s_crlf.IndexOf("U") != -1) {
						OnB01ButtonReadEPCClick(null, null);
						break;
					}
					IsReceiveDataWork = false;
					Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
						this.B01TextBoxEPC.Text = this.ReaderService.DeleteCRLFandHandler(s_crlf, 'Q');
					}));
					DisplayInfoMsg("RX", s_crlf);
					break;
				case CommandStates.TID:
					if (s_crlf.IndexOf("U") != -1) {
						OnB01ButtonTIDClick(null, null);
						break;
					}
					IsReceiveDataWork = false;
					Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
						this.B01TextBoxTID.Text = this.ReaderService.DeleteCRLFandHandler(s_crlf, 'R'); 
					}));
					DisplayInfoMsg("RX", s_crlf);
					break;
				case CommandStates.SELECT: 
					IsReceiveDataWork = false;
					DisplayInfoMsg("RX", s_crlf);
					break;
				case CommandStates.PASSWORD:
					IsReceiveDataWork = false;
					DisplayInfoMsg("RX", s_crlf);
					break;
				case CommandStates.MULTI:
					if (s_crlf.Equals("\nU\r\n")) IsReceiveDataWork = false;
					DisplayInfoMsg("RX", s_crlf);
					DisplayStatisticsMsg(this.ReaderService.DeleteCRLFandHandler(s_crlf, 'U'));
					break;
				case CommandStates.READ:
					Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
						this.B01TextBoxRead.Text = this.ReaderService.DeleteCRLFandHandler(s_crlf, 'R');
						ErrorCodeCheck(CommandStates.READ, (s_crlf.IndexOf('R') != -1) ? "" : this.B01TextBoxRead.Text);
					}));
					DisplayInfoMsg("RX", s_crlf);
					IsReceiveDataWork = false;
					break;
				case CommandStates.WRITE:
					Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
						ErrorCodeCheck(CommandStates.WRITE, this.ReaderService.RemoveCRLF(s_crlf));
					}));
					DisplayInfoMsg("RX", s_crlf);
					IsReceiveDataWork = false;
					break;
				case CommandStates.LOCK:
					Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
						ErrorCodeCheck(CommandStates.KILL, this.ReaderService.RemoveCRLF(s_crlf));
					}));
					DisplayInfoMsg("RX", s_crlf);
					IsReceiveDataWork = false;
					break;
				case CommandStates.KILL:
					Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
						ErrorCodeCheck(CommandStates.KILL, this.ReaderService.RemoveCRLF(s_crlf));
					}));
					DisplayInfoMsg("RX", s_crlf);
					IsReceiveDataWork = false;
					break;
				case CommandStates.B02_MULTI:
				case CommandStates.B02_MULTI_Q:
					if (s_crlf.Equals("\nU\r\n") || s_crlf.Equals("\nX\r\n")) {
						IsReceiveDataWork = false;
						if (s_crlf.Equals("\nX\r\n"))
							Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
								ErrorCodeCheck(CommandStates.B02_MULTI_Q, "X");
							}));
					}
					B02DisplayInfoMsg("RX", s_crlf);
					B02DisplayStatisticsMsg(this.ReaderService.DeleteCRLFandHandler(s_crlf, 'U'));
					break;
				case CommandStates.B02_MULTIREAD:
				case CommandStates.B02_MULTIREAD_Q:
					if (s_crlf.Equals("\nU\r\n") || s_crlf.Equals("\nX\r\n")) { 
						IsReceiveDataWork = false;
						if (s_crlf.Equals("\nX\r\n"))
							Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
								ErrorCodeCheck(CommandStates.B02_MULTIREAD_Q, "X");
							}));
					}
					
					B02DisplayInfoMsg("RX", s_crlf);
					B02DisplayStatisticsMsg(this.ReaderService.DeleteCRLFandHandler(s_crlf, 'U'));
					break;
				case CommandStates.INFO:
				case CommandStates.DEFAULT: 
					break;
				default: 
					DisplayInfoMsg("RX", s_crlf); 
					break;
			}
			ClearMemory();
		}
		private void DoInfoWork() {
			byte[] b;
			string s;

			try {
				this.DoProcess = CommandStates.INFO;
				this.ReaderService.Send(this.ReaderService.Command_V(), ReaderService.Module.CommandType.Normal);
				b = this.ReaderService.Receive();
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
					if (b == null) {
						MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "the Reader not response." : "Reader沒有回應");
						this.FirmwareVersion.Text = "N/A";
					}
					else {
						s = this.ReaderService.RemoveCRLF(this.ReaderService.BytesToString(b));
						this.FirmwareVersion.Text = s;
						this.VersionFW = ReaderService.Module.Check(this.ReaderService.HexStringToInt(s.Substring(1, 4)));

						switch (this.VersionFW) {
							case ReaderService.Module.Version.FI_R3008:
								UIControPackets.Clear();
								UIControPackets.Add(new UIControl(GroupStatus.TAG_SELECT, false));
								UIControPackets.Add(new UIControl(GroupStatus.READER_SET, false));
								UIControPackets.Add(new UIControl(GroupStatus.B02Q, false));
								UIControPackets.Add(new UIControl(GroupStatus.B02READ, false));
								UIControlStatus(UIControPackets, true);
								MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "the Reader version isn't support Select, compound multi command" : "此版本Reader不支援Select, 複合multi指令操作");
								break;
							case ReaderService.Module.Version.FI_R300A_C1:
								UIControPackets.Clear();
								UIControPackets.Add(new UIControl(GroupStatus.B02Q, false));
								UIControPackets.Add(new UIControl(GroupStatus.B02READ, false));
								UIControlStatus(UIControPackets, true);
								MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "the Reader version isn't support compound multi command" : "此版本Reader不支援複合multi指令操作");
								break;
							case ReaderService.Module.Version.FI_R300A_C2:
							case ReaderService.Module.Version.FI_R300T_D1:
							case ReaderService.Module.Version.FI_R300T_D2:
							case ReaderService.Module.Version.FI_R300S:
								break;
							case ReaderService.Module.Version.FI_RXXXX:
								UIControPackets.Clear();
								UIControPackets.Add(new UIControl(GroupStatus.TAG_SELECT, false));
								UIControPackets.Add(new UIControl(GroupStatus.TAG_KILL, false));
								UIControPackets.Add(new UIControl(GroupStatus.TAG_LOCK, false));
								UIControPackets.Add(new UIControl(GroupStatus.READER_SET, false));
								UIControlStatus(UIControPackets, true);
								MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Unknown the Reader module version" : "未知的Reader版本");
								break;
						}
					}
				}));

				this.ReaderService.Send(this.ReaderService.Command_S(), ReaderService.Module.CommandType.Normal);
				b = this.ReaderService.Receive();
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
					if (b == null) {
						MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "the Reader not response." : "Reader沒有回應");
						this.TBReaderID.Text = "N/A";
					}
					else
						this.TBReaderID.Text = this.ReaderService.RemoveCRLF(this.ReaderService.BytesToString(b));
				}));
			}
			catch (System.Exception ex) {
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => { MessageShow(ex.Message); }));
			}
			
		}
		private bool DoSelectWork() {
			if (this.B01TextBoxBitAddress.Text == "") {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Bit address is null." : "位元位址不得為空");
				this.IsFocus = true;
				this.B01TextBoxBitAddress.Focus();
				goto SELECTEXIT;
			}
			if (Convert.ToInt32(B01TextBoxBitAddress.Text, 16) > 0x3FFF) {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Bit address value over range: 0 ~ 0x3FFF" : "位元位址值超出規範值: 0 ~ 0x3FFF");
				this.IsFocus = true;
				this.B01TextBoxBitAddress.Focus();
				goto SELECTEXIT;
			}
			if (B01TextBoxBitLength.Text == "") {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Bit length is null." : "位元長度不得為空");
				this.IsFocus = true;
				this.B01TextBoxBitLength.Focus();
				goto SELECTEXIT;
			}
			if (Convert.ToInt32(B01TextBoxBitLength.Text, 16) > 0x60 || Convert.ToInt32(B01TextBoxBitLength.Text, 16) < 1) {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Bit length value over range: 0x01 ~ 0x60." : "位元長度值超出規範值: 0x01 ~ 0x60");
				this.IsFocus = true;
				this.B01TextBoxBitLength.Focus();
				goto SELECTEXIT;
			}
			if (B01TextBoxBitData.Text == "") {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Data is null." : "資料內容不得為空.");
				this.IsFocus = true;
				this.B01TextBoxBitData.Focus();
				goto SELECTEXIT;
			}
			int nLength = B01TextBoxBitData.Text.Length;
			int nBitsLength = Convert.ToInt32(B01TextBoxBitLength.Text, 16);
			int nMax = nLength * 4;
			int nMin = nLength * 4 - 3;
			if ((nBitsLength < nMin) || (nBitsLength > nMax)) {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ?
					string.Format("Bit length and data field isn't match, data={0}, the length value range is: 0x{1} ~ 0x{2}",
					B01TextBoxBitData.Text,
					nMin.ToString("X2", new CultureInfo("en-us")),
					nMax.ToString("X2", new CultureInfo("en-us"))) :
					String.Format("位元長度與資料不符合,資料={0},對應的長度值範圍應為: 0x{1} ~ 0x{2}",
					B01TextBoxBitData.Text,
					nMin.ToString("X2", new CultureInfo("en-us")),
					nMax.ToString("X2", new CultureInfo("en-us"))));
				this.IsFocus = true;
				this.B01TextBoxBitLength.Focus();
				goto SELECTEXIT;
			}

			DoSendReceiveWork(this.ReaderService.Command_T(((ComboBoxItem)B01ComboBoxMemBankSelect.SelectedItem).Tag as string,
				B01TextBoxBitAddress.Text, B01TextBoxBitLength.Text, B01TextBoxBitData.Text), CommandStates.SELECT);
			while (IsReceiveDataWork) { Thread.Sleep(2); }
			Thread.Sleep(16);
			return true;

		SELECTEXIT:
			return false;
		}
		private void DoSendReceiveWork(byte[] command, CommandStates process) {
			if (!IsReceiveDataWork) {
				IsReceiveDataWork = true;
				if (process == CommandStates.MULTI)
					RunCount_++;

				this.DoProcess = process;
				this.ReaderService.Send(command, ReaderService.Module.CommandType.Normal);
				DisplayInfoMsg("TX", this.ReaderService.BytesToString(command));
			}
		}
		private void DoB02SendReceiveWork(byte[] command, CommandStates process) {
			if (!IsReceiveDataWork) {
				IsReceiveDataWork = true;
				if (process == CommandStates.B02_MULTI || process == CommandStates.B02_MULTIREAD ||
                    process == CommandStates.B02_MULTI_Q || process == CommandStates.B02_MULTIREAD_Q)
					B02RunCount_++;

				this.DoProcess = process;
				this.ReaderService.Send(command, ReaderService.Module.CommandType.Normal);
				B02DisplayInfoMsg("TX", this.ReaderService.BytesToString(command));
			}
		}
		private void DoRepeatWork(object sender, EventArgs e) {
			switch (this.DoProcess) {
				case CommandStates.EPC:
					DoSendReceiveWork(this.ReaderService.Command_Q(), CommandStates.EPC);
					break;
				case CommandStates.TID:
					DoSendReceiveWork(this.ReaderService.Command_R("2", "0", "4"), CommandStates.TID);
					break;
				case CommandStates.MULTI:
					DoSendReceiveWork(this.ReaderService.Command_U(), CommandStates.MULTI);
					break;
				case CommandStates.B02_MULTI:
					DoB02SendReceiveWork(this.ReaderService.Command_U(), CommandStates.B02_MULTI);
					break;
				case CommandStates.B02_MULTI_Q:
					DoB02SendReceiveWork(this.ReaderService.Command_U(((ComboBoxItem)this.B02ComboBoxQ.SelectedItem).Tag as string), CommandStates.B02_MULTI_Q);
					break;
				case CommandStates.B02_MULTIREAD:
					DoB02SendReceiveWork(this.ReaderService.Command_UR(null,
						((ComboBoxItem)this.B02ComboBoxMemBankSetting.SelectedItem).Tag as string,
						this.B02TextBoxSettingAddress.Text, this.B02TextBoxSettingLength.Text), CommandStates.B02_MULTIREAD); 
					break;
				case CommandStates.B02_MULTIREAD_Q:
					DoB02SendReceiveWork(this.ReaderService.Command_UR(((ComboBoxItem)this.B02ComboBoxQ.SelectedItem).Tag as string,
						((ComboBoxItem)this.B02ComboBoxMemBankSetting.SelectedItem).Tag as string,
						this.B02TextBoxSettingAddress.Text, this.B02TextBoxSettingLength.Text), CommandStates.B02_MULTIREAD_Q);
					break;
			}
		}
		private void DisplayInfoMsg(string str, string data) {
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
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
					else
						itm.Content = string.Format("{0} [{1}] - {2}", DateTime.Now.ToString("yy/MM/dd H:mm:ss.fff"), str, this.ReaderService.ShowCRLF(data));
				}
				else {
					if (data == null) 
						itm.Content = string.Format("[{0}] - ", str);
					else
						if (IsReceiveDataWork)					
							itm.Content = this.ReaderService.ShowCRLF(data);
						else
							itm.Content = string.Format("{0}  -- {1}", this.ReaderService.ShowCRLF(data), DateTime.Now.ToString("H:mm:ss.fff"));
				}			

				if (this.mListBox.Items.Count > 1000)
					this.mListBox.Items.Clear();

				this.mListBox.Items.Add(itm);
				this.mListBox.ScrollIntoView(this.mListBox.Items[this.mListBox.Items.Count - 1]);
				itm = null;
			}));
		}
		private void DisplayInfoMsg(string str, string buffer, string oldV, string newV) {
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
				ListBoxItem itm = new ListBoxItem();
				if (str == "TX") {
					this.IsDateTimeStamp = true; 
					itm.Foreground = Brushes.SeaGreen; 
				}
				else 
					itm.Foreground = Brushes.DarkRed;
				string s;
				if (this.IsDateTimeStamp) {
					if (str == "RX") this.IsDateTimeStamp = false;
					s = string.Format("{0} [{1}] - {2}", DateTime.Now.ToString("yy/MM/dd H:mm:ss.fff"), str, this.ReaderService.ShowCRLF(buffer));
				}
				else
					s = this.ReaderService.ShowCRLF(buffer);
				s = s.Replace(oldV, newV);
				itm.Content = s;

				if (this.mListBox.Items.Count > 1000)
					this.mListBox.Items.Clear();

				this.mListBox.Items.Add(itm);
				this.mListBox.ScrollIntoView(this.mListBox.Items[this.mListBox.Items.Count - 1]);
				itm = null;
			}));
		}
		private void DisplayStatisticsMsg(string str) {
			MemBankEPC oldMemBankEPC, newMemBankEPC;
			bool bCompare = false;
			int number;
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
				if (str.Length > 8 && ReaderService.CRC16(ReaderService.HexStringToBytes(str)) == 0x1D0F) {

					newMemBankEPC = new MemBankEPC() {
						_PC = str.Substring(0, 4),
						_EPC = str.Substring(4, str.Length - 8),
						_CRC16 = str.Substring(str.Length - 4, 4)
					};
					if (this.B01ListViewQU.Items.Count > 0) {
						for (int j = 0; j < this.B01ListViewQU.Items.Count; j++) {
							oldMemBankEPC = this.B01ListViewQU.Items.GetItemAt(j) as MemBankEPC;
							number = Convert.ToInt32(oldMemBankEPC._Count);
							if (oldMemBankEPC._CRC16 == newMemBankEPC._CRC16) {
								number++;
								oldMemBankEPC._Count = number.ToString();
								bCompare = true;
								//break;
							}
							else
								bCompare = false;
							oldMemBankEPC._Percentage = string.Format("{0}%", (int)(number * 100 / this.RunCount_));
							this.B01ListViewQU.Items.Refresh();
							if (bCompare) break;
						}
					}
					if (!bCompare) {
						newMemBankEPC._Count = "1";
						this.B01ListViewQU.Items.Add(newMemBankEPC);
						TagCount_++;
						this.B01TextBlockCount.Text = TagCount_.ToString();
					}
				}
				else {
					if (this.B01ListViewQU.Items.Count > 0) {
						for (int j = 0; j < this.B01ListViewQU.Items.Count; j++) {
							oldMemBankEPC = this.B01ListViewQU.Items.GetItemAt(j) as MemBankEPC;
							number = Convert.ToInt32(oldMemBankEPC._Count);
							oldMemBankEPC._Percentage = string.Format("{0}%", (int)(number * 100 / this.RunCount_));
							this.B01ListViewQU.Items.Refresh();
						}
					}
				}
				newMemBankEPC = null;
			}));
		}
		private void B02DisplayInfoMsg(string str, string data) {
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
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
					else
						itm.Content = string.Format("{0} [{1}] - {2}", DateTime.Now.ToString("yy/MM/dd H:mm:ss.fff"), str, this.ReaderService.ShowCRLF(data));
				}
				else {
					if (data == null)
						itm.Content = string.Format("[{0}] - ", str);
					else
						if (IsReceiveDataWork)
							itm.Content = this.ReaderService.ShowCRLF(data);
						else
							itm.Content = string.Format("{0}  -- {1}", this.ReaderService.ShowCRLF(data), DateTime.Now.ToString("H:mm:ss.fff"));
				}
				if (this.B02ListBox.Items.Count > 1000)
					this.B02ListBox.Items.Clear();

				this.B02ListBox.Items.Add(itm);
				
				//this.B02ListBox.UpdateLayout();
				//this.B02ListBox.ScrollIntoView(this.B02ListBox.Items.Count - 1);
				itm = null;
			}));
		}
		private void B02DisplayStatisticsMsg(string str) {
			B02MemBankEPC oldBank, newBank;
			bool bCompare = false, isCRC = false, isEPC = false;
			int number;
			string[] data = str.Split(',');

			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
				if (str.Length > 8) {
					if (ReaderService.CRC16(ReaderService.HexStringToBytes(data[0])) == 0x1D0F) isCRC = true;
					else isCRC = false;

					if (!IsMenuTagCount && !isCRC) return;
					if (!IsMenuTagCount && B02CheckBoxRead.IsChecked.Value) 
						if (data[1].Length <= 1) return;
					newBank = new B02MemBankEPC() {
						_B02PC = data[0].Substring(0, 4),
						_B02EPC = data[0].Substring(4, data[0].Length - 8),
						_B02CRC16 = (isCRC) ? data[0].Substring(data[0].Length - 4, 4) : data[0].Substring(data[0].Length - 4, 4) + " x",
						_B02Read = (data.Length > 1) ? data[1] : ""
					};
					if (this.B02ListViewQU.Items.Count > 0) {
						for (int j = 0; j < this.B02ListViewQU.Items.Count; j++) {
							oldBank = this.B02ListViewQU.Items.GetItemAt(j) as B02MemBankEPC;
							number = Convert.ToInt32(oldBank._B02Count);
							if (oldBank._B02CRC16.Equals(newBank._B02CRC16) && oldBank._B02Read.Equals(newBank._B02Read)) {							
								number++;
								oldBank._B02Count = number.ToString();
								bCompare = true;	
							}
							else
								bCompare = false;
							if (!isEPC && oldBank._B02EPC.Equals(newBank._B02EPC)) isEPC = true;
							oldBank._B02Percentage = string.Format("{0}%", (int)(number * 100 / this.B02RunCount_));
							this.B02ListViewQU.Items.Refresh();
							if (bCompare) break;
						}
					}
					if (!bCompare) {
						newBank._B02Count = "1";
						this.B02ListViewQU.Items.Add(newBank);
						if (!isEPC)
							this.B02TagCount_++;
						this.B02TextBlockCount.Text = B02TagCount_.ToString();
					}
				}
				else {
					if (this.B02ListViewQU.Items.Count > 0) {
						for (int j = 0; j < this.B02ListViewQU.Items.Count; j++) {
							oldBank = this.B02ListViewQU.Items.GetItemAt(j) as B02MemBankEPC;
							number = Convert.ToInt32(oldBank._B02Count);
							oldBank._B02Percentage = string.Format("{0}%", (int)(number * 100 / this.B02RunCount_));
							this.B02ListViewQU.Items.Refresh();
						}
					}
				}
				this.B02TextBlockRunCount.Text = this.B02RunCount_.ToString();
			}));
		}
		private void MessageShow(string s) { this.LabelMessage.Text = s; }	
		private void TextBox_PreviewKeyUp(object sender, KeyEventArgs e) {
			TextBox tbox = (TextBox)sender;
			Regex regex = new Regex(@"[0-9a-fA-F]");
			if (regex.IsMatch(e.Key.ToString())) {
				if ((ValidationStates)ValidationState[tbox.Name] != ValidationStates.OK) tbox.Style = (Style)FindResource("textBoxNormalStyle");
				ValidationState[tbox.Name] = ValidationStates.OK;
				if (tbox.Name == "B01TextBoxBitAddress") {
					if (tbox.Text == "" || Convert.ToInt32(tbox.Text, 16) > 0x3FFF) {
						tbox.Style = (Style)FindResource("textBoxErrorStyle");
						ValidationState[tbox.Name] = ValidationStates.ERROR;
					}
					else {
						tbox.Style = (Style)FindResource("textBoxNormalStyle");
						ValidationState[tbox.Name] = ValidationStates.OK;
					}
				}
				else if (tbox.Name == "B01TextBoxBitLength") {
					if (tbox.Text == "" || Convert.ToInt32(tbox.Text, 16) > 0x60 || Convert.ToInt32(tbox.Text, 16) < 1) {
						tbox.Style = (Style)FindResource("textBoxErrorStyle");
						ValidationState[tbox.Name] = ValidationStates.ERROR;
					}
					else {
						tbox.Style = (Style)FindResource("textBoxNormalStyle");
						ValidationState[tbox.Name] = ValidationStates.OK;
					}
				}
				else if (tbox.Name == "B01TextBoxBitData") {
					try {
						int nLength = tbox.Text.Length;
						int nBitsLength = Convert.ToInt32(B01TextBoxBitLength.Text, 16);
						int nMax = nLength * 4;
						int nMin = nLength * 4 - 3;
						if (tbox.Text == "" || nBitsLength < nMin || nBitsLength > nMax) {
							tbox.Style = (Style)FindResource("textBoxErrorStyle");
							ValidationState[tbox.Name] = ValidationStates.ERROR;
							MessageShow((this.Culture.IetfLanguageTag == "en-US") ?
								string.Format("Bit length and data field isn't match, data={0}, the length value range is: 0x{1} ~ 0x{2}",
								tbox.Text,
								nMin.ToString("X2", new CultureInfo("en-us")),
								nMax.ToString("X2", new CultureInfo("en-us"))) :
								String.Format("位元長度與資料不符合,資料={0},對應的長度值範圍應為: 0x{1} ~ 0x{2}",
								tbox.Text,
								nMin.ToString("X2", new CultureInfo("en-us")),
								nMax.ToString("X2", new CultureInfo("en-us"))));
						}
						else {
							tbox.Style = (Style)FindResource("textBoxNormalStyle");
							ValidationState[tbox.Name] = ValidationStates.OK;
							MessageShow("");
						}
					}
					catch (System.Exception ex) {
						MessageShow(ex.Message);
					}
					
				}
				else if (tbox.Name == "B01TextBoxSettingAddress" || tbox.Name == "B02TextBoxSettingAddress") {
					if (tbox.Text == "" || Convert.ToInt32(tbox.Text, 16) > 0x3FFF) {
						tbox.Style = (Style)FindResource("textBoxErrorStyle");
						ValidationState[tbox.Name] = ValidationStates.ERROR;
					}
					else {
						tbox.Style = (Style)FindResource("textBoxNormalStyle");
						ValidationState[tbox.Name] = ValidationStates.OK;
					}
				}
				else if (tbox.Name == "B01TextBoxSettingLength" || tbox.Name == "B02TextBoxSettingLength") {
					if (tbox.Text == "" || Convert.ToInt32(tbox.Text, 16) > 0x20 || Convert.ToInt32(tbox.Text, 16) < 1) {
						tbox.Style = (Style)FindResource("textBoxErrorStyle");
						ValidationState[tbox.Name] = ValidationStates.ERROR;
					}
					else {
						tbox.Style = (Style)FindResource("textBoxNormalStyle");
						ValidationState[tbox.Name] = ValidationStates.OK;
					}
				}
				else if (tbox.Name == "B01TextBoxWrite") {
					int nLength = tbox.Text.Length;
					int nWordsLength = Convert.ToInt32(this.B01TextBoxSettingLength.Text, 16);
					if (nWordsLength * 4 != nLength) {
						tbox.Style = (Style)FindResource("textBoxErrorStyle");
						ValidationState[tbox.Name] = ValidationStates.ERROR;
					}
					else {
						tbox.Style = (Style)FindResource("textBoxNormalStyle");
						ValidationState[tbox.Name] = ValidationStates.OK;
					}
				}
				else if (tbox.Name == "B01TextBoxMask" || tbox.Name == "B01TextBoxAction" || tbox.Name == "B01TextBoxAccessPassword" || tbox.Name == "B01TextBoxKillPassword") {
					if (tbox.Text == "") {
						tbox.Style = (Style)FindResource("textBoxErrorStyle");
						ValidationState[tbox.Name] = ValidationStates.ERROR;
					}
					else {
						tbox.Style = (Style)FindResource("textBoxNormalStyle");
						ValidationState[tbox.Name] = ValidationStates.OK;
					}
				}	
			}
			else {
				tbox.Style = (Style)FindResource("textBoxErrorStyle");
				ValidationState[tbox.Name] = ValidationStates.ERROR;
				tbox.UpdateLayout();
				Image errImg = (Image)tbox.Template.FindName("ErrorImage", tbox);
				e.Handled = true;
			}
		}
		private void TextBoxLostFocusValidation(object sender, RoutedEventArgs e) {
			TextBox tbox = (TextBox)sender;
			tbox.Style = (Style)FindResource("textBoxDefaultStyle");
		}
		private void TextBoxGotFocusValidation(object sender, RoutedEventArgs e) {
			TextBox tbox = (TextBox)sender;
			if (tbox.Name == "B01TextBoxBitAddress") {
				if (this.IsFocus) {
					this.IsFocus = false;
					tbox.Style = (Style)FindResource("textBoxFocusStyle");
					ValidationState[tbox.Name] = ValidationStates.FOCUS;
				}
				else {
					if (tbox.Text == "" || Convert.ToInt32(tbox.Text, 16) > 0x3FFF) {
						tbox.Style = (Style)FindResource("textBoxErrorStyle");
						ValidationState[tbox.Name] = ValidationStates.ERROR;
					}
					else {
						tbox.Style = (Style)FindResource("textBoxNormalStyle");
						ValidationState[tbox.Name] = ValidationStates.OK;
					}
				}
			}
			else if (tbox.Name == "B01TextBoxBitLength") {
				if (this.IsFocus) {
					this.IsFocus = false;
					tbox.Style = (Style)FindResource("textBoxFocusStyle");
					ValidationState[tbox.Name] = ValidationStates.FOCUS;
				}
				else {
					if (tbox.Text == "" || Convert.ToInt32(tbox.Text, 16) > 0x60 || Convert.ToInt32(tbox.Text, 16) < 1) {
						tbox.Style = (Style)FindResource("textBoxErrorStyle");
						ValidationState[tbox.Name] = ValidationStates.ERROR;
					}
					else {
						tbox.Style = (Style)FindResource("textBoxNormalStyle");
						ValidationState[tbox.Name] = ValidationStates.OK;
					}
				}			
			}
			else if (tbox.Name == "B01TextBoxBitData") {
				try {
					int nLength = tbox.Text.Length;
					int nBitsLength = (nLength == 0) ? 0 : Convert.ToInt32(B01TextBoxBitLength.Text, 16);
					int nMax = nLength * 4;
					int nMin = nLength * 4 - 3;
					if (this.IsFocus) {
						this.IsFocus = false;
						tbox.Style = (Style)FindResource("textBoxFocusStyle");
						ValidationState[tbox.Name] = ValidationStates.FOCUS;
					}
					else {
						if (tbox.Text == "" || nBitsLength < nMin || nBitsLength > nMax) {
							tbox.Style = (Style)FindResource("textBoxErrorStyle");
							ValidationState[tbox.Name] = ValidationStates.ERROR;
						}
						else {
							tbox.Style = (Style)FindResource("textBoxNormalStyle");
							ValidationState[tbox.Name] = ValidationStates.OK;
						}
					}
				}
				catch (System.Exception ex) {
					MessageShow(ex.Message);
				}
			}
			else if (tbox.Name == "B01TextBoxSettingAddress" || tbox.Name == "B02TextBoxSettingAddress") {
				if (this.IsFocus) {
					this.IsFocus = false;
					tbox.Style = (Style)FindResource("textBoxFocusStyle");
					ValidationState[tbox.Name] = ValidationStates.FOCUS;
				}
				else {
					if (tbox.Text == "" || Convert.ToInt32(tbox.Text, 16) > 0x3FFF) {
						tbox.Style = (Style)FindResource("textBoxErrorStyle");
						ValidationState[tbox.Name] = ValidationStates.ERROR;
					}
					else {
						tbox.Style = (Style)FindResource("textBoxNormalStyle");
						ValidationState[tbox.Name] = ValidationStates.OK;
					}
				}	
			}
			else if (tbox.Name == "B01TextBoxSettingLength" || tbox.Name == "B02TextBoxSettingLength") {
				if (this.IsFocus) {
					this.IsFocus = false;
					tbox.Style = (Style)FindResource("textBoxFocusStyle");
					ValidationState[tbox.Name] = ValidationStates.FOCUS;
				}
				else {
					if (tbox.Text == "" || Convert.ToInt32(tbox.Text, 16) > 0x20 || Convert.ToInt32(tbox.Text, 16) < 1) {
						tbox.Style = (Style)FindResource("textBoxErrorStyle");
						ValidationState[tbox.Name] = ValidationStates.ERROR;
					}
					else {
						tbox.Style = (Style)FindResource("textBoxNormalStyle");
						ValidationState[tbox.Name] = ValidationStates.OK;
					}
				}
				
			}
			else if (tbox.Name == "B01TextBoxWrite") {
				int nDataLength = tbox.Text.Length;
				if (this.B01TextBoxSettingLength.Text == "") { this.B01TextBoxSettingLength.Focus(); goto GotFocus; }
				int nWordsLength = Convert.ToInt32(this.B01TextBoxSettingLength.Text, 16);
				if (this.IsFocus) {
					this.IsFocus = false;
					tbox.Style = (Style)FindResource("textBoxFocusStyle");
					ValidationState[tbox.Name] = ValidationStates.FOCUS;
				}
				else {
					if (nWordsLength * 4 == nDataLength) {
						tbox.Style = (Style)FindResource("textBoxNormalStyle");
						ValidationState[tbox.Name] = ValidationStates.OK;
						goto GotFocus;
					}
					else {
						tbox.Style = (Style)FindResource("textBoxErrorStyle");
						ValidationState[tbox.Name] = ValidationStates.ERROR;
					}
				}
				
			}
			else if (tbox.Name == "B01TextBoxMask" || tbox.Name == "B01TextBoxAction" || tbox.Name == "B01TextBoxAccessPassword" || tbox.Name == "B01TextBoxKillPassword") {
				if (this.IsFocus) {
					this.IsFocus = false;
					tbox.Style = (Style)FindResource("textBoxFocusStyle");
					ValidationState[tbox.Name] = ValidationStates.FOCUS;
				}
				else {
					if (tbox.Text == "") {
						tbox.Style = (Style)FindResource("textBoxErrorStyle");
						ValidationState[tbox.Name] = ValidationStates.ERROR;
					}
					else {
						tbox.Style = (Style)FindResource("textBoxNormalStyle");
						ValidationState[tbox.Name] = ValidationStates.OK;
					}
				}	
			}	

		GotFocus: ;
		}
		private int LockPayloadMask(int mask, int index) {
			if (mask == 0) return 0x0;
			else {
				this.Action_ |= (mask - 1) << index;
				if (((mask - 1) & 1) == 0) mask = 2;
				else mask = 3;
				return mask << index;
			}
		}
		private void ErrorCodeCheck(CommandStates process, string s) {
			switch (process) {
				case CommandStates.EPC: break;
				case CommandStates.TID: break;
				case CommandStates.MULTI: break;
				case CommandStates.READ:
				case CommandStates.KILL:
				case CommandStates.LOCK:
				case CommandStates.WRITE:
				case CommandStates.B02_MULTI:
				case CommandStates.B02_MULTI_Q:
				case CommandStates.B02_MULTIREAD_Q:
					if (s == "") break;
					if (s == "0") MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "0: other error." : "0: 其他未知的錯誤");
					else if (s == "3") MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "3: The specified memory location does not exist or the EPC length field is not supported." : "3: 寫入的記憶體位置不存在或內容長度超出範圍");
					else if (s == "4") MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "4: The specified memory location is locked and/or per-malocked." : "4: 此標籤記憶體已鎖住或永久鎖住");
					else if (s == "B") MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "B: The Tag has insufficient power." : "B: 標籤Power不足，無法進行寫入操作");
					else if (s == "F") MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "F: Non-specific error." : "F: 此標籤不支援錯誤規範碼(Error-specific)");
					else if (s[0] == 'Z') MessageShow((this.Culture.IetfLanguageTag == "en-US") ? string.Format("{0}: {1} chars written to the memory.", s, s.Substring(1, 2)) : string.Format("{0}: {1} 字元已寫入", s, s.Substring(1, 2)));
					else if (s == "W") MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "W: The Tag does not exist or no reply." : "W: 沒有標籤在Reader存取範圍內");
					else if (s == "K") MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "K: The Tag does not exist or no reply." : "K: 沒有標籤在Reader存取範圍內");
					else if (s == "E") MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "E: Error." : "E: 執行指令過程標籤回覆錯誤");
					else if (s == "X") MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "X: Command error or not support." : "X: 傳送指令格式錯誤或未支援");
					else if (s == "L<OK>") MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "L<OK>: The Tag has been successfully lock." : "L<OK>: 標籤Lock成功");
					else if (s == "W<OK>") MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "W<OK>: The Tag has been successfully written." : "W<OK>: 標籤寫入成功");
					else if (s.Length >= 2 && s.Substring(0, 2) == "3Z") MessageShow((this.Culture.IetfLanguageTag == "en-US") ? string.Format("{0}: error code and {1} chars has been written.", s, s.Substring(2, 2)) : string.Format("{0}: 錯誤碼且 {1} 字元已寫入", s, s.Substring(2, 2)));
					else MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "N/A: Not applicable." : "N/A: 不適用此版本Utility");
					break;
				case CommandStates.DEFAULT:
				default: break;
			}
		}
		

		#region #Group EPC/TID
		private void OnB01CheckBoxMultiChecked(object sender, RoutedEventArgs e) {
			this.B01ButtonReadEPC_U.Visibility = Visibility.Visible;
			this.B01ButtonReadEPC.Visibility = Visibility.Collapsed;
			this.B01ButtonReadEPC_U.Content = (this.Culture.IetfLanguageTag == "en-US") ? "EPC (U)" : "讀取EPC(U)";
		}
		private void OnB01CheckBoxMultiUnchecked(object sender, RoutedEventArgs e) {
			this.B01ButtonReadEPC.Visibility = Visibility.Visible;
			this.B01ButtonReadEPC_U.Visibility = Visibility.Collapsed;
			this.B01ButtonReadEPC.Content = (this.Culture.IetfLanguageTag == "en-US") ? "EPC (Q)" : "讀取EPC(Q)";
		}
		private void OnB01CheckBoxRepeatChecked(object sender, RoutedEventArgs e) { }
		private void OnB01CheckBoxRepeatTIDChecked(object sender, RoutedEventArgs e) { }
		private void OnB01ButtonReadEPCClick(object sender, RoutedEventArgs e) {
			MessageShow("");
			if (!IsRepeat) {
				if (B01CheckBoxMulti.IsChecked.Value == true) {
					if (B01CheckBoxRepeat.IsChecked.Value == true) {
						this.IsRepeat = true;
						this.B01ButtonReadEPC.Content = (this.Culture.IetfLanguageTag == "en-US") ? "Stop" : "停止讀取";
						UITempControPackets.Clear();
						UITempControPackets.Add(new UIControl(GroupStatus.TAG_SELECT, false));
						UITempControPackets.Add(new UIControl(GroupStatus.READ_WRITE, false));
						UITempControPackets.Add(new UIControl(GroupStatus.TAG_LOCK, false));
						UITempControPackets.Add(new UIControl(GroupStatus.TAG_KILL, false));
						UITempControPackets.Add(new UIControl(GroupStatus.READER_SET, false));
						UITempControPackets.Add(new UIControl(GroupStatus.MULTI_CHECK, false));
						UITempControPackets.Add(new UIControl(GroupStatus.EPC_REPEAT, false));
						UITempControPackets.Add(new UIControl(GroupStatus.TID_REPEAT, false));
						UITempControPackets.Add(new UIControl(GroupStatus.BTN_CLR, false));
						UITempControPackets.Add(new UIControl(GroupStatus.BTN_SAVE, false));
						UITempControPackets.Add(new UIControl(GroupStatus.BTN_TID, false));
						UITempControPackets.Add(new UIControl(GroupStatus.CB_CULTURE, false));
						UIControlStatus(UITempControPackets, true);
						this.DoProcess = CommandStates.MULTI;
						//this.RepeatEvent.Interval = TimeSpan.FromMilliseconds(64);
						this.RepeatEvent.Start();
					}
					else
						DoSendReceiveWork(this.ReaderService.Command_U(), CommandStates.MULTI);
				}
				else {
					if (B01CheckBoxRepeat.IsChecked.Value == true) {
						this.IsRepeat = true;
						this.B01ButtonReadEPC.Content = (this.Culture.IetfLanguageTag == "en-US") ? "Stop" : "停止讀取";
						UITempControPackets.Clear();
						UITempControPackets.Add(new UIControl(GroupStatus.TAG_SELECT, false));
						UITempControPackets.Add(new UIControl(GroupStatus.READ_WRITE, false));
						UITempControPackets.Add(new UIControl(GroupStatus.TAG_LOCK, false));
						UITempControPackets.Add(new UIControl(GroupStatus.TAG_KILL, false));
						UITempControPackets.Add(new UIControl(GroupStatus.READER_SET, false));
						UITempControPackets.Add(new UIControl(GroupStatus.MULTI_CHECK, false));
						UITempControPackets.Add(new UIControl(GroupStatus.EPC_REPEAT, false));
						UITempControPackets.Add(new UIControl(GroupStatus.TID_REPEAT, false));
						UITempControPackets.Add(new UIControl(GroupStatus.BTN_CLR, false));
						UITempControPackets.Add(new UIControl(GroupStatus.BTN_SAVE, false));
						UITempControPackets.Add(new UIControl(GroupStatus.BTN_TID, false));
						UITempControPackets.Add(new UIControl(GroupStatus.CB_CULTURE, false));
						UIControlStatus(UITempControPackets, true);
						this.DoProcess = CommandStates.EPC;
						//this.RepeatEvent.Interval = TimeSpan.FromMilliseconds(64);
						this.RepeatEvent.Start();
					}
					else
						DoSendReceiveWork(this.ReaderService.Command_Q(), CommandStates.EPC);
				}
			}
			else {
				this.IsRepeat = false;
				UIControlStatus(UITempControPackets, false);
				UIControlStatus(UIControPackets, true);
				this.B01ButtonReadEPC.Content = (this.Culture.IetfLanguageTag == "en-US") ? "EPC (Q)" : "讀取EPC(Q)";
				this.RepeatEvent.Stop();
			}

		}
		private void OnB01ButtonReadEPCClick_U(object sender, RoutedEventArgs e) {
			MessageShow("");
			if (!IsRepeat) {
				if (B01CheckBoxMulti.IsChecked.Value == true) {
					if (B01CheckBoxRepeat.IsChecked.Value == true) {
						this.IsRepeat = true;
						this.B01ButtonReadEPC_U.Content = (this.Culture.IetfLanguageTag == "en-US") ? "Stop" : "停止讀取";
						UITempControPackets.Clear();
						UITempControPackets.Add(new UIControl(GroupStatus.TAG_SELECT, false));
						UITempControPackets.Add(new UIControl(GroupStatus.READ_WRITE, false));
						UITempControPackets.Add(new UIControl(GroupStatus.TAG_LOCK, false));
						UITempControPackets.Add(new UIControl(GroupStatus.TAG_KILL, false));
						UITempControPackets.Add(new UIControl(GroupStatus.READER_SET, false));
						UITempControPackets.Add(new UIControl(GroupStatus.MULTI_CHECK, false));
						UITempControPackets.Add(new UIControl(GroupStatus.EPC_REPEAT, false));
						UITempControPackets.Add(new UIControl(GroupStatus.TID_REPEAT, false));
						UITempControPackets.Add(new UIControl(GroupStatus.BTN_CLR, false));
						UITempControPackets.Add(new UIControl(GroupStatus.BTN_SAVE, false));
						UITempControPackets.Add(new UIControl(GroupStatus.BTN_TID, false));
						UITempControPackets.Add(new UIControl(GroupStatus.CB_CULTURE, false));
						UIControlStatus(UITempControPackets, true);
						this.DoProcess = CommandStates.MULTI;
						//this.RepeatEvent.Interval = TimeSpan.FromMilliseconds(64);
						this.RepeatEvent.Start();
					}
					else DoSendReceiveWork(this.ReaderService.Command_U(), CommandStates.MULTI);
				}
				else {
					if (B01CheckBoxRepeat.IsChecked.Value == true) {
						this.IsRepeat = true;
						this.B01ButtonReadEPC_U.Content = (this.Culture.IetfLanguageTag == "en-US") ? "Stop" : "停止讀取";
						UITempControPackets.Clear();
						UITempControPackets.Add(new UIControl(GroupStatus.TAG_SELECT, false));
						UITempControPackets.Add(new UIControl(GroupStatus.READ_WRITE, false));
						UITempControPackets.Add(new UIControl(GroupStatus.TAG_LOCK, false));
						UITempControPackets.Add(new UIControl(GroupStatus.TAG_KILL, false));
						UITempControPackets.Add(new UIControl(GroupStatus.READER_SET, false));
						UITempControPackets.Add(new UIControl(GroupStatus.MULTI_CHECK, false));
						UITempControPackets.Add(new UIControl(GroupStatus.EPC_REPEAT, false));
						UITempControPackets.Add(new UIControl(GroupStatus.TID_REPEAT, false));
						UITempControPackets.Add(new UIControl(GroupStatus.BTN_CLR, false));
						UITempControPackets.Add(new UIControl(GroupStatus.BTN_SAVE, false));
						UITempControPackets.Add(new UIControl(GroupStatus.BTN_TID, false));
						UITempControPackets.Add(new UIControl(GroupStatus.CB_CULTURE, false));
						UIControlStatus(UITempControPackets, true);
						this.DoProcess = CommandStates.EPC;
						//this.RepeatEvent.Interval = TimeSpan.FromMilliseconds(64);
						this.RepeatEvent.Start();
					}
					else DoSendReceiveWork(this.ReaderService.Command_Q(), CommandStates.EPC);
				}
			}
			else {
				this.IsRepeat = false;
				UIControlStatus(UITempControPackets, false);
				UIControlStatus(UIControPackets, true);
				this.B01ButtonReadEPC_U.Content = (this.Culture.IetfLanguageTag == "en-US") ? "EPC (U)" : "讀取EPC(U)";
				this.RepeatEvent.Stop();
			}
		}
		private void OnB01ButtonTIDClick(object sender, RoutedEventArgs e) {
			MessageShow("");
			if (!IsRepeat) {
				if (B01CheckBoxRepeatTID.IsChecked.Value == true) {
					this.IsRepeat = true;
					this.B01ButtonTID.Content = (this.Culture.IetfLanguageTag == "en-US") ? "Stop" : "停止讀取";
					UITempControPackets.Clear();
					UITempControPackets.Add(new UIControl(GroupStatus.TAG_SELECT, false));
					UITempControPackets.Add(new UIControl(GroupStatus.READ_WRITE, false));
					UITempControPackets.Add(new UIControl(GroupStatus.TAG_LOCK, false));
					UITempControPackets.Add(new UIControl(GroupStatus.TAG_KILL, false));
					UITempControPackets.Add(new UIControl(GroupStatus.READER_SET, false));
					UITempControPackets.Add(new UIControl(GroupStatus.MULTI_CHECK, false));
					UITempControPackets.Add(new UIControl(GroupStatus.EPC_REPEAT, false));
					UITempControPackets.Add(new UIControl(GroupStatus.TID_REPEAT, false));
					UITempControPackets.Add(new UIControl(GroupStatus.BTN_CLR, false));
					UITempControPackets.Add(new UIControl(GroupStatus.BTN_SAVE, false));
					UITempControPackets.Add(new UIControl(GroupStatus.BTN_EPC, false));
					UITempControPackets.Add(new UIControl(GroupStatus.BTN_EPCU, false));
					UITempControPackets.Add(new UIControl(GroupStatus.CB_CULTURE, false));
					UIControlStatus(UITempControPackets, true);
					this.DoProcess = CommandStates.TID;
					//this.RepeatEvent.Interval = TimeSpan.FromMilliseconds(100);
					this.RepeatEvent.Start();
				}
				else
					DoSendReceiveWork(this.ReaderService.Command_R("2", "0", "4"), CommandStates.TID);
			}
			else {
				this.IsRepeat = false;
				UIControlStatus(UITempControPackets, false);
				UIControlStatus(UIControPackets, true);
				this.B01ButtonTID.Content = (this.Culture.IetfLanguageTag == "en-US") ? "TID" : "TID查詢";
				this.RepeatEvent.Stop();
			}
		}
		private void OnB01TextBoxTIDTextChanged(object sender, TextChangedEventArgs e) { }
		#endregion

		#region #Group Pre-Process
		private void OnB01CheckBoxSelectPreCommandChecked(object sender, RoutedEventArgs e) { }
		private void OnB01ComboBoxMemBankSelectDownClosed(object sender, EventArgs e) { }
		private void OnB01ComboBoxMemBankSelectChanged(object sender, SelectionChangedEventArgs e) { }
		private void OnB01CheckBoxAccessPreCommandChecked(object sender, RoutedEventArgs e) { }
		#endregion

		#region #Group Read/Write
		private void OnB01ComboBoxMemBankSettingDownClosed(object sender, EventArgs e) {
			switch ((sender as ComboBox).SelectedIndex) {
				case 0: B01TextBoxSettingAddress.Text = "0"; B01TextBoxSettingLength.Text = "4"; break;
				case 1: B01TextBoxSettingAddress.Text = "2"; B01TextBoxSettingLength.Text = "6"; break;
				case 2: B01TextBoxSettingAddress.Text = "0"; B01TextBoxSettingLength.Text = "4"; break;
				case 3: B01TextBoxSettingAddress.Text = "0"; B01TextBoxSettingLength.Text = "1"; break;
				case 4: B01TextBoxSettingAddress.Text = ""; B01TextBoxSettingLength.Text = ""; break;
				case 5: B01TextBoxSettingAddress.Text = "0"; B01TextBoxSettingLength.Text = "2"; break;
				case 6: B01TextBoxSettingAddress.Text = "2"; B01TextBoxSettingLength.Text = "2"; break;
			}
		}
		private void OnB01ButtonWriteClick(object sender, RoutedEventArgs e) {
			MessageShow("");
			(sender as Button).IsEnabled = false;

			if (this.B01TextBoxSettingAddress.Text == "") {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ?
						"Address is null" :
						"寫入位址不得為空");
				this.IsFocus = true;
				this.B01TextBoxSettingAddress.Focus();
				goto WRITEEXIT;
			}
			if (Convert.ToInt32(this.B01TextBoxSettingAddress.Text, 16) > 0x3FFF) {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ?
					"Address value over range: 0 ~ 0x3FFF" :
					"寫入位址值超出規範值: 0 ~ 0x3FFF");
				this.IsFocus = true;
				this.B01TextBoxSettingAddress.Focus();
				goto WRITEEXIT;
			}
			if (this.B01TextBoxSettingLength.Text == "") {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ?
					"Length is null" :
					"位元組長度不得為空");
				this.IsFocus = true;
				this.B01TextBoxSettingLength.Focus();
				goto WRITEEXIT;
			}
			if (Convert.ToInt32(this.B01TextBoxSettingLength.Text, 16) > 0x20 || Convert.ToInt32(this.B01TextBoxSettingLength.Text, 16) < 1) {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Length value over range: 1 ~ 0x20" : "位元組長度值超出規範值:1 ~ 0x20");
				this.IsFocus = true;
				this.B01TextBoxSettingLength.Focus();
				goto WRITEEXIT;
			}
			if (this.B01TextBoxWrite.Text == "") {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Data field is null" : "寫入資料不得為空");
				this.IsFocus = true;
				this.B01TextBoxWrite.Focus();
				goto WRITEEXIT;
			}

			int nDataLength = this.B01TextBoxWrite.Text.Length;
			int nWordsLength = Convert.ToInt32(this.B01TextBoxSettingLength.Text, 16);
			if (nWordsLength * 4 != nDataLength) {
				if (nWordsLength * 4 > nDataLength)
					MessageShow((this.Culture.IetfLanguageTag == "en-US") ?
						String.Format("Length and data field is not match, data field must be fill {0} char.", (nWordsLength * 4 - nDataLength)) :
						String.Format("位元組長度值與資料內容不匹配，資料欄位應再填入{0}字元", (nWordsLength * 4 - nDataLength)));
				else
					MessageShow((this.Culture.IetfLanguageTag == "en-US") ?
						String.Format("Length and data field is not match, data field must be remove {0} char.", (nDataLength - nWordsLength * 4)) :
						String.Format("位元組長度值與資料內容不匹配，資料欄位應再移除{0}字元", (nDataLength - nWordsLength * 4)));
				this.IsFocus = true;
				this.B01TextBoxWrite.Focus();
				goto WRITEEXIT;
			}

			if (this.B01CheckBoxSelectPreCommand.IsChecked.Value == true)
				if (DoSelectWork() == false) goto WRITEEXIT;

			if (this.B01CheckBoxAccessPreCommand.IsChecked.Value == true) {
				if (this.B01TextBoxAccessPassword.Text == "") {
					MessageShow((this.Culture.IetfLanguageTag == "en-US") ?
						"Can not be ' '(null character), Please setting the Access password." :
						"Access 密碼不得為空");
					this.IsFocus = true;
					this.B01TextBoxAccessPassword.Focus();
					goto WRITEEXIT;
				}
				DoSendReceiveWork(this.ReaderService.Command_P(this.B01TextBoxAccessPassword.Text), CommandStates.PASSWORD);
				while (IsReceiveDataWork) { Thread.Sleep(2); }
				Thread.Sleep(16);
			}

			DoSendReceiveWork(this.ReaderService.Command_W(((ComboBoxItem)B01ComboBoxMemBankSetting.SelectedItem).Tag as string,
				B01TextBoxSettingAddress.Text, B01TextBoxSettingLength.Text, B01TextBoxWrite.Text), CommandStates.WRITE);

		WRITEEXIT:
			(sender as Button).IsEnabled = true;
		}
		private void OnB01ButtonReadClick(object sender, RoutedEventArgs e) {
			MessageShow("");
			(sender as Button).IsEnabled = false;

			if (this.B01TextBoxSettingAddress.Text == "") {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Address is null" : "讀取位址不得為空");
				this.IsFocus = true;
				this.B01TextBoxSettingAddress.Focus();
				goto READEXIT;
			}
			if (Convert.ToInt32(this.B01TextBoxSettingAddress.Text, 16) > 0x3FFF) {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Address value over range: 0 ~ 0x3FFF" : "讀取位址值超出規範值: 0 ~ 0x3FFF");
				this.IsFocus = true;
				this.B01TextBoxSettingAddress.Focus();
				goto READEXIT;
			}
			if (this.B01TextBoxSettingLength.Text == "") {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Length is null" : "讀取長度不得為空");
				this.IsFocus = true;
				this.B01TextBoxSettingLength.Focus();
				goto READEXIT;
			}
			if (Convert.ToInt32(this.B01TextBoxSettingLength.Text, 16) > 0x20 || Convert.ToInt32(this.B01TextBoxSettingLength.Text, 16) < 1) {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Length value over range: 1 ~ 0x20" : "讀取長度值超出規範值:1 ~ 0x20");
				this.IsFocus = true;
				this.B01TextBoxSettingLength.Focus();
				goto READEXIT;
			}

			if (this.B01CheckBoxSelectPreCommand.IsChecked.Value == true)
				if (DoSelectWork() == false) goto READEXIT;

			if (this.B01CheckBoxAccessPreCommand.IsChecked.Value == true) {
				if (this.B01TextBoxAccessPassword.Text == "") {
					MessageShow((this.Culture.IetfLanguageTag == "en-US") ?
						"Access command is pre-processed, the field is null" :
						"Access指令已設為預處理指令，密碼欄位不得為空");
					this.IsFocus = true;
					this.B01TextBoxAccessPassword.Focus();
					goto READEXIT;
				}
				this.B01TextBoxAccessPassword.Text = this.ReaderService.MakesUpZero(this.B01TextBoxAccessPassword.Text, 8);
				DoSendReceiveWork(this.ReaderService.Command_P(this.B01TextBoxAccessPassword.Text), CommandStates.PASSWORD);
				while (IsReceiveDataWork) { Thread.Sleep(2); }
				Thread.Sleep(16);
			}
			DoSendReceiveWork(this.ReaderService.Command_R(((ComboBoxItem)this.B01ComboBoxMemBankSetting.SelectedItem).Tag as string,
				this.B01TextBoxSettingAddress.Text, this.B01TextBoxSettingLength.Text), CommandStates.READ);

		READEXIT:
			(sender as Button).IsEnabled = true;
		}
		#endregion

		#region #Group Lock
		private void OnB01ButtonLockClick(object sender, RoutedEventArgs e) {
			MessageShow("");
			(sender as Button).IsEnabled = false;

			if (this.B01CheckBoxSelectPreCommand.IsChecked.Value == true)
				if (DoSelectWork() == false) goto LOCKEXIT;

			if (this.B01CheckBoxAccessPreCommand.IsChecked.Value == true) {
				if (this.B01TextBoxAccessPassword.Text == "") {
					MessageShow((this.Culture.IetfLanguageTag == "en-US") ?
						"Access command is pre-processed, the field is null" :
						"Access指令已設為預處理指令，密碼欄位不得為空");
					this.IsFocus = true;
					this.B01TextBoxAccessPassword.Focus();
					goto LOCKEXIT;
				}
				this.B01TextBoxAccessPassword.Text = this.ReaderService.MakesUpZero(this.B01TextBoxAccessPassword.Text, 8);
				DoSendReceiveWork(this.ReaderService.Command_P(this.B01TextBoxAccessPassword.Text), CommandStates.PASSWORD);
			}

			if (this.IsMenuLockGroup) {
				if (this.B01TextBoxMask.Text == "") {
					MessageShow((this.Culture.IetfLanguageTag == "en-US") ?
					"Lock Mask is null." :
					"Mask設定不得為空");
					this.IsFocus = true;
					this.B01TextBoxMask.Focus();
					goto LOCKEXIT;
				}
				if (this.B01TextBoxAction.Text == "") {
					MessageShow((this.Culture.IetfLanguageTag == "en-US") ?
					"Lock Action is null." :
					"Action設定不得為空");
					this.IsFocus = true;
					this.B01TextBoxAction.Focus();
					goto LOCKEXIT;
				}
				DoSendReceiveWork(this.ReaderService.Command_L(this.B01TextBoxMask.Text, this.B01TextBoxAction.Text), CommandStates.LOCK);
			}
			else {
				this.Mask_ = 0;
				this.Action_ = 0;

				this.Mask_ |= LockPayloadMask(Convert.ToInt32(((ComboBoxItem)B01ComboBoxLockKillPwd.SelectedItem).Tag.ToString()), 8);
				this.Mask_ |= LockPayloadMask(Convert.ToInt32(((ComboBoxItem)B01ComboBoxLockAccessPwd.SelectedItem).Tag.ToString()), 6);
				this.Mask_ |= LockPayloadMask(Convert.ToInt32(((ComboBoxItem)B01ComboBoxLockEPC.SelectedItem).Tag.ToString()), 4);
				this.Mask_ |= LockPayloadMask(Convert.ToInt32(((ComboBoxItem)B01ComboBoxLockTID.SelectedItem).Tag.ToString()), 2);
				this.Mask_ |= LockPayloadMask(Convert.ToInt32(((ComboBoxItem)B01ComboBoxLockUser.SelectedItem).Tag.ToString()), 0);

				DoSendReceiveWork(this.ReaderService.Command_L(this.Mask_.ToString("X"), this.Action_.ToString("X")), CommandStates.LOCK);
			}

		LOCKEXIT:
			(sender as Button).IsEnabled = true;
		}
		#endregion

		#region #Group Kill
		private void OnB01ButtonKillClick(object sender, RoutedEventArgs e) {
			MessageShow("");
			(sender as Button).IsEnabled = false;

			if (this.B01TextBoxKillPassword.Text == "") {
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ?
					"Kill password is null." :
					"寫入銷毀密碼不得為空");
				this.B01TextBoxKillPassword.Focus();
				goto KILLEXIT;
			}
			DoSendReceiveWork(this.ReaderService.Command_K(this.B01TextBoxKillPassword.Text, 0x30), CommandStates.KILL);

		KILLEXIT:
			(sender as Button).IsEnabled = true;
		}
		#endregion

		#region #Group Set Regulation
		private void OnB01ButtonSettingClick(object sender, RoutedEventArgs e) {
			MessageShow("");
			this.DoProcess = CommandStates.DEFAULT;           
            this.RegulationDialog_ = new RegulationDialog(this.ReaderService, this.VersionFW, this.ComboBoxCulture.SelectedItem as CultureInfo);
            this.RegulationDialog_.ShowDialog();
            DoInfoWork();
		}
		#endregion

		#region #Group ListBox
		private void OnB01ListBoxMenuItemDeleteAllClick(object sender, RoutedEventArgs e) {
			this.mListBox.Items.Clear();
		}
		private void OnB01ListBoxMenuItemDeleteRangeClick(object sender, RoutedEventArgs e) {
			ListBox lb = new ListBox();
			object[] ob = new object[this.mListBox.SelectedItems.Count];
			this.mListBox.SelectedItems.CopyTo(ob, 0);

			foreach (object obj in ob)
				this.mListBox.Items.Remove(obj);
		}
		private void OnB01ButtonClearClick(object sender, RoutedEventArgs e) {
			this.TagCount_ = 0; 
			this.RunCount_ = 0;
			this.B01TextBlockCount.Text = "";
			this.B01ListViewQU.Items.Clear();
		}
		private void OnB01ButtonSaveClick(object sender, RoutedEventArgs e) {
			string stFilePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase +
								"\\Record-" + DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss") + ".txt";
			StreamWriter swStream;
			MemBankEPC mbe;
			string str = "";

			if (File.Exists(stFilePath))
				swStream = new StreamWriter(stFilePath);
			else
				swStream = File.CreateText(stFilePath);

			for (int i = 0; i < this.B01ListViewQU.Items.Count; i++) {
				mbe = this.B01ListViewQU.Items.GetItemAt(i) as MemBankEPC;
				str = mbe._PC + ",\t" + mbe._EPC + ",\t" + mbe._CRC16 + ",\t" + mbe._Count;
				swStream.WriteLine(str);
				str = "";
			}

			swStream.Flush();
			swStream.Close();
			System.Diagnostics.Process.Start("notepad.exe", stFilePath);
		}
		#endregion

		#region #Group Border
		[DllImport("user32.dll")]
		public static extern Boolean GetWindowRect(IntPtr hWnd, ref Rectangle bounds);
		private void OnCloseConnectClick(object sender, RoutedEventArgs e) {
			if (CheckBoxStatus.Tag.ToString() == "True") {
				this.ReaderService.Close();
				this.CheckBoxStatus.Tag = "False";
				this.InfoThread.Join();
				GroupStatusControl(GroupStatus.ALL, false);
				MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Disconnected" : "已中斷連線");
				this.TextBlockStatus.Text = "";
				this.FirmwareVersion.Text = "";
				this.TBReaderID.Text = "";
			}
		}
		private void OnOpenConnectClick(object sender, RoutedEventArgs e) {
			if (CheckBoxStatus.Tag.ToString() == "False") {
				this.ConnectDialog = new ConnectDialog();
				this.ConnectDialog.ShowDialog();
				if (ConnectDialog.DialogResult.HasValue && !ConnectDialog.DialogResult.Value) this.Close();
				else if (ConnectDialog.DialogResult.HasValue && ConnectDialog.DialogResult.Value) {
					this.ReaderService = this.ConnectDialog.GetService();
					this.SerialPort = this.ReaderService.GetSerialPort();
                    this.ReaderService.CombineDataReceiveEvent += new ReaderService.CombineDataHandler(DoReceiveDataWork);
                    this.ReaderService.RawDataReceiveEvent += new ReaderService.RawDataHandler(DoRawReceiveDataWork);
                    this.CheckBoxStatus.Tag = "True";
					this.TextBlockStatus.Text = string.Format("{0} ({1},{2},{3},{4})",
																this.SerialPort.PortName,
																this.SerialPort.BaudRate,
																this.SerialPort.DataBits,
																this.SerialPort.Parity,
																this.SerialPort.StopBits);
					this.InfoThread = new Thread(DoInfoWork);
					this.InfoThread.IsBackground = true;
					this.InfoThread.Start();

					GroupStatusControl(GroupStatus.ALL, true);
					MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Reconnect" : "已重新連線");
				}
			}
		}
		private void OnComboBoxCultureSelectionChanged(object sender, SelectionChangedEventArgs e) {
			CultureInfo selected_culture = this.ComboBoxCulture.SelectedItem as CultureInfo;
			this.Culture = selected_culture;

			if (B01CheckBoxMulti.IsChecked.Value == true)
				this.B01ButtonReadEPC_U.Content = (this.Culture.IetfLanguageTag == "en-US") ? "EPC (U)" : "讀取EPC(U)";
			else
				this.B01ButtonReadEPC.Content = (this.Culture.IetfLanguageTag == "en-US") ? "EPC (Q)" : "讀取EPC(Q)";
			this.B01ButtonTID.Content = (this.Culture.IetfLanguageTag == "en-US") ? "TID" : "TID查詢";

			if (Properties.Resources.Culture != null && !Properties.Resources.Culture.Equals(selected_culture))
				CulturesHelper.ChangeCulture(selected_culture);
		}
		private void OnMenuMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
			var menu = sender as TextBlock;
			this.MenuLockGroup.Header = (this.Culture.IetfLanguageTag == "en-US") ?
				(this.IsMenuLockGroup == false) ? "Set Lock to Engineering Mode" : "Set Lock to User Mode" :
				(this.IsMenuLockGroup == false) ? "切換Lock設定為工程模式" : "切換Lock設定為使用者模式";
			this.MenuTagCount.Header = (this.Culture.IetfLanguageTag == "en-US") ?
				(this.IsMenuTagCount == true) ? "to Normal Tag Count" : "to Debug Tag Count" :
				(this.IsMenuTagCount == true) ? "切換至正常模式計數" : "切換至除錯模式計數";
		}
		private void OnMenuBugRepoetClick(object sender, RoutedEventArgs e) {
			Process[] process = Process.GetProcessesByName("notepad");
			//Rectangle bounds;
			//GetWindowRect(process[0].MainWindowHandle, ref bounds);

			/*Bitmap bm = new Bitmap(
				//System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width, 
				//System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height
				bounds.Width,
				bounds.Height,
				System.Drawing.Imaging.PixelFormat.Format32bppArgb
				);
			System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bm);
			g.CopyFromScreen(new System.Drawing.Point(0, 0),
				new System.Drawing.Point(0, 0),
				new System.Drawing.Size(
					System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width, 
					System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height));

			string strFilePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "report";
			string strFileName = "\\ScreenShot_" + DateTime.Now.ToString("yyyyMMdd_hhmmss") + ".jpg";
			if (!Directory.Exists(strFilePath))
				Directory.CreateDirectory(strFilePath);
			string strFullPath = strFilePath + strFileName;
			bm.Save(strFullPath);*/
		}
		private void OnMenuLockGroupClick(object sender, RoutedEventArgs e) {
			if (this.IsMenuLockGroup) {
				this.IsMenuLockGroup = false;
				this.B01TextBoxMask.Visibility = Visibility.Collapsed;
				this.B01TextBoxAction.Visibility = Visibility.Collapsed;
				this.B01GridLockMemoryBlock.IsEnabled = true;
			}
			else {
				this.IsMenuLockGroup = true;
				this.B01TextBoxMask.Visibility = Visibility.Visible;
				this.B01TextBoxAction.Visibility = Visibility.Visible;
				this.B01GridLockMemoryBlock.IsEnabled = false;
			}
		}
		private void OnMenuTagCountClick(object sender, RoutedEventArgs e) {
			if (IsMenuTagCount) IsMenuTagCount = false;
			else IsMenuTagCount = true;
		}
		#endregion

		

		#region Pager
		private int CurrentPage = 1;
		private void SlidePage(Border oldVisual, Border newVisual) {
			oldVisual.Margin = new Thickness(1100, 0, 0, 0);
			newVisual.Margin = new Thickness(0);
		}
		private void OnPreviousButtonClick(object sender, RoutedEventArgs e) {
			switch (this.CurrentPage) {
				case 2:
					SlidePage(Border02, Border01);
					this.CurrentPage--;
					break;
			}
		}
		private void OnNextButtonClick(object sender, RoutedEventArgs e) {
			switch (this.CurrentPage) {
				case 1:
					SlidePage(Border01, Border02);
					this.CurrentPage++;
					break;
			}
		}
		#endregion


		#region #Border02
		private void OnB02ComboBoxMemBankSettingDownClosed(object sender, EventArgs e) {
			switch ((sender as ComboBox).SelectedIndex) {
				case 0: B02TextBoxSettingAddress.Text = "0"; B02TextBoxSettingLength.Text = "4"; break;
				case 1: B02TextBoxSettingAddress.Text = "2"; B02TextBoxSettingLength.Text = "6"; break;
				case 2: B02TextBoxSettingAddress.Text = "0"; B02TextBoxSettingLength.Text = "4"; break;
				case 3: B02TextBoxSettingAddress.Text = "0"; B02TextBoxSettingLength.Text = "1"; break;
				case 4: B02TextBoxSettingAddress.Text = ""; B02TextBoxSettingLength.Text = ""; break;
				case 5: B02TextBoxSettingAddress.Text = "0"; B02TextBoxSettingLength.Text = "2"; break;
				case 6: B02TextBoxSettingAddress.Text = "2"; B02TextBoxSettingLength.Text = "2"; break;
			}
		}
		private void OnB02ButtonReadURClick(object sender, RoutedEventArgs e) {
			MessageShow("");
			if (B02CheckBoxRead.IsChecked.Value) {
				if (this.B02TextBoxSettingAddress.Text == "") {
					MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Address is null" : "讀取位址不得為空");
					this.B02TextBoxSettingAddress.Focus();
					goto B02EXIT;
				}
				if (Convert.ToInt32(this.B02TextBoxSettingAddress.Text, 16) > 0x3FFF) {
					MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Address value over range: 0 ~ 0x3FFF" : "讀取位址值超出規範值: 0 ~ 0x3FFF");
					this.B02TextBoxSettingAddress.Focus();
					goto B02EXIT;
				}
				if (this.B02TextBoxSettingLength.Text == "") {
					MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Length is null" : "讀取長度不得為空");
					this.B02TextBoxSettingLength.Focus();
					goto B02EXIT;
				}
				if (Convert.ToInt32(this.B02TextBoxSettingLength.Text, 16) > 0x20 || Convert.ToInt32(this.B02TextBoxSettingLength.Text, 16) < 1) {
					MessageShow((this.Culture.IetfLanguageTag == "en-US") ? "Length value over range: 1 ~ 0x20" : "讀取長度值超出規範值:1 ~ 0x20");
					this.B02TextBoxSettingLength.Focus();
					goto B02EXIT;
				}
			}

			if (!IsB02Repeat) {	
				if (B02Repeat.IsChecked.Value) {
					this.IsB02Repeat = true;
					this.B02ButtonReadUR.Content = (this.Culture.IetfLanguageTag == "en-US") ? "Stop" : "停止讀取";
					UITempControPackets.Clear();
					UITempControPackets.Add(new UIControl(GroupStatus.B02PREVIOUS, false));
					UITempControPackets.Add(new UIControl(GroupStatus.B02NEXT, false));
					UITempControPackets.Add(new UIControl(GroupStatus.B02BTN_CLR, false));
					UITempControPackets.Add(new UIControl(GroupStatus.B02BTN_SAVE, false));
					UITempControPackets.Add(new UIControl(GroupStatus.CB_CULTURE, false));
					UITempControPackets.Add(new UIControl(GroupStatus.B02Q, false));
					UITempControPackets.Add(new UIControl(GroupStatus.B02READ, false));
					UITempControPackets.Add(new UIControl(GroupStatus.B02CONTINUE, false));
					UIControlStatus(UITempControPackets, true);
					if (B02CheckBoxRead.IsChecked.Value)
						if (B02CheckQ.IsChecked.Value) this.DoProcess = CommandStates.B02_MULTIREAD_Q;
						else this.DoProcess = CommandStates.B02_MULTIREAD;
					else
						if (B02CheckQ.IsChecked.Value)  this.DoProcess = CommandStates.B02_MULTI_Q;
						else this.DoProcess = CommandStates.B02_MULTI;
					this.RepeatEvent.Start();
				}
				else {
					if (B02CheckBoxRead.IsChecked.Value)
						if (B02CheckQ.IsChecked.Value)
							DoB02SendReceiveWork(this.ReaderService.Command_UR(((ComboBoxItem)this.B02ComboBoxQ.SelectedItem).Tag as string,
								((ComboBoxItem)this.B02ComboBoxMemBankSetting.SelectedItem).Tag as string,
								this.B02TextBoxSettingAddress.Text, this.B02TextBoxSettingLength.Text), CommandStates.B02_MULTIREAD_Q); 
						else
							DoB02SendReceiveWork(this.ReaderService.Command_UR(null,
								((ComboBoxItem)this.B02ComboBoxMemBankSetting.SelectedItem).Tag as string,
								this.B02TextBoxSettingAddress.Text, this.B02TextBoxSettingLength.Text), CommandStates.B02_MULTIREAD); 
					else
						if (B02CheckQ.IsChecked.Value)
							DoB02SendReceiveWork(this.ReaderService.Command_U(((ComboBoxItem)this.B02ComboBoxQ.SelectedItem).Tag as string), CommandStates.B02_MULTI_Q);
						else
							DoB02SendReceiveWork(this.ReaderService.Command_U(), CommandStates.B02_MULTI);
				}
			}
			else {
				this.IsB02Repeat = false;
				UIControlStatus(UITempControPackets, false);
				UIControlStatus(UIControPackets, true);
				this.B02ButtonReadUR.Content = (this.Culture.IetfLanguageTag == "en-US") ? "MultiRead" : "MultiRead";
				this.RepeatEvent.Stop();
			}
		B02EXIT: ;
		}
		private void OnB02TextBlockCountClearClick(object sender, RoutedEventArgs e) {
			this.B02TextBlockCount.Text = "";
			this.B02TagCount_ = 0; 
			this.B02RunCount_ = 0;
		}
		private void OnB02ButtonSaveClick(object sender, RoutedEventArgs e) {
			string stFilePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase +
								"\\Record-" + DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss") + ".txt";
			StreamWriter swStream;
			B02MemBankEPC mbe;
			string str = "";

			if (File.Exists(stFilePath))
				swStream = new StreamWriter(stFilePath);
			else
				swStream = File.CreateText(stFilePath);

			for (int i = 0; i < this.B02ListViewQU.Items.Count; i++) {
				mbe = this.B02ListViewQU.Items.GetItemAt(i) as B02MemBankEPC;
				str = mbe._B02PC + ",\t" + mbe._B02EPC + ",\t" + mbe._B02CRC16 + ",\t" + mbe._B02Read + ",\t" + mbe._B02Count;
				swStream.WriteLine(str);
				str = "";
			}

			swStream.Flush();
			swStream.Close();
			System.Diagnostics.Process.Start("notepad.exe", stFilePath);
		}
		private void OnB02ButtonClearClick(object sender, RoutedEventArgs e) {
			this.B02TagCount_ = 0; 
			this.B02RunCount_ = 0;
			this.B02TextBlockCount.Text = "";
			this.B02TextBlockRunCount.Text = "";
			this.B02ListViewQU.Items.Clear();
		}
		private void OnB02ListBoxMenuItemDeleteRangeClick(object sender, RoutedEventArgs e) {
			ListBox lb = new ListBox();
			object[] ob = new object[this.B02ListBox.SelectedItems.Count];
			this.B02ListBox.SelectedItems.CopyTo(ob, 0);

			foreach (object obj in ob)
				this.B02ListBox.Items.Remove(obj);
		}
		private void OnB02ListBoxMenuItemDeleteAllClick(object sender, RoutedEventArgs e) {
			this.B02ListBox.Items.Clear();
		}

		GridViewColumnHeader _lastHeaderClicked = null;
		ListSortDirection _lastDirection = ListSortDirection.Ascending;
		private void Sort(string sortBy, ListSortDirection direction) {
			ICollectionView dataView = CollectionViewSource.GetDefaultView(this.B02ListViewQU.ItemsSource != null ? this.B02ListViewQU.ItemsSource : this.B02ListViewQU.Items);

			dataView.SortDescriptions.Clear();
			SortDescription sd = new SortDescription(sortBy, direction);
			dataView.SortDescriptions.Add(sd);
			dataView.Refresh();
		}
		private void OnB02ListViewQUHeaderClick(object sender, RoutedEventArgs e) {
			GridViewColumnHeader headerClicked = e.OriginalSource as GridViewColumnHeader;
			ListSortDirection direction;

			if (headerClicked != null) {
				if (headerClicked.Role != GridViewColumnHeaderRole.Padding) {
					if (headerClicked != _lastHeaderClicked)
						direction = ListSortDirection.Ascending;
					else {
						if (_lastDirection == ListSortDirection.Ascending)
							direction = ListSortDirection.Descending;
						else
							direction = ListSortDirection.Ascending;
					}

					string header = ((Binding)headerClicked.Column.DisplayMemberBinding).Path.Path;
					Sort(header, direction);

					if (direction == ListSortDirection.Ascending)
						headerClicked.Column.HeaderTemplate = Resources["HeaderTemplateArrowUp"] as DataTemplate;
					else
						headerClicked.Column.HeaderTemplate = Resources["HeaderTemplateArrowDown"] as DataTemplate;

					if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
						_lastHeaderClicked.Column.HeaderTemplate = null;

					_lastHeaderClicked = headerClicked;
					_lastDirection = direction;
				}
			}
		}
		#endregion
		

		/*private void OnButtonBitDataHelpClick(object sender, RoutedEventArgs e)
		{
			HelpUserControlData.Visibility = Visibility.Visible;
			HelpUserControlData.Data = "123456798";
			HelpUserControlData.X = B01TextBoxBitData.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0, 0)).X;
			HelpUserControlData.Y = B01TextBoxBitData.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0, 0)).Y;
		}*/
	}
}
