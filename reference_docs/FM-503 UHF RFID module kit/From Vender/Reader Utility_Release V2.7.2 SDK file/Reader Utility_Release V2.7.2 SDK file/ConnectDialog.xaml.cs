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
using System.IO.Ports;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Threading;
using ReaderServiceDLL;

namespace ReaderUtility
{
	/// <summary>
	/// Interaction logic for ConnectDialog.xaml
	/// </summary>
	public partial class ConnectDialog : Window
	{
		private ReaderService	ReaderService;
		private Thread			SearchThread;


		public ConnectDialog() {
			InitializeComponent();

			this.ButtonConnect.IsEnabled = false;
			this.ButtonEnter.IsEnabled = false;
			this.ComboBoxReader.IsEnabled = false;
			this.ComboBoxReader.Focusable = false;
			this.SearchThread = new Thread(DoSearchWork);
			this.SearchThread.IsBackground = true;
			this.SearchThread.Start();
		}


		private void DoSearchWork() {
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action<String>(showOnTBMSG1), "正在搜尋RFID Reader...");
			ObservableCollection<string> oc = new ObservableCollection<string>();
			foreach (ReaderService.ReaderSearch rs in ReaderService.ReaderSearch.GetReader())
            {
				if (rs != null)
                {
                    Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
                        try
                        {
                            this.ReaderService = new ReaderService();
                            showOnTBMSG2(string.Format("驗證{0}", rs.Name));
                            this.ReaderService.Open(rs.Name, 38400, (Parity)Enum.Parse(typeof(Parity), "None", true), 8, (StopBits)Enum.Parse(typeof(StopBits), "One", true));
                            this.ReaderService.Send(this.ReaderService.Command_V());
                            byte[] b = this.ReaderService.Receive(100);
                            if (b != null)
                            {
                                string s = this.ReaderService.RemoveCRLF(this.ReaderService.BytesToString(b));
                                if (s.Contains("V"))
                                    oc.Add(string.Format("{0} –{1}[{2}]", rs.Name, rs.Description, s));
                            }
                            this.ReaderService.Close();
                            this.ReaderService = null;
                            Thread.Sleep(100);
                        }
                        catch (System.Exception ex)
                        {
                            showOnTBMSG2(ex.Message);
                        }
                    }));	
				}			
			}

			Dispatcher.Invoke(DispatcherPriority.Normal, new Action<ObservableCollection<string>>(collectionData), oc);
			Thread.Sleep(100);

			if (oc.Count == 0) {
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
					Dispatcher.Invoke(DispatcherPriority.Normal, new Action<String>(showOnTBMSG1), "");
					Dispatcher.Invoke(DispatcherPriority.Normal, new Action<String>(showOnTBMSG2), "開啟通訊埠失敗或未偵測到RFID Reader.");
					this.ComboBoxReader.IsEnabled = true;
				}));
			}
			else if (oc.Count == 1) {
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
					Dispatcher.Invoke(DispatcherPriority.Normal, new Action<String>(showOnTBMSG1), "正在連接指定的RFID Reader...");
					Dispatcher.Invoke(DispatcherPriority.Normal, new Action<String>(showOnTBMSG2), "");
					Thread.Sleep(100);

					string[] words = (ComboBoxReader.SelectedItem as string).Split(' ');
					this.ReaderService = new ReaderService();
					this.ReaderService.Open(words[0], 38400, (Parity)Enum.Parse(typeof(Parity), "None", true), 8, (StopBits)Enum.Parse(typeof(StopBits), "One", true));

					Dispatcher.Invoke(DispatcherPriority.Normal, new Action<String>(showOnTBMSG1), "");
					if (this.ReaderService.IsOpen()) {
						Dispatcher.Invoke(DispatcherPriority.Normal, new Action<String>(showOnTBMSG2), "已連接RFID Reader. (" + words[0] + ")");
						this.ButtonEnter.IsEnabled = true;
						this.ButtonConnect.IsEnabled = false;
					}
					else
						Dispatcher.Invoke(DispatcherPriority.Normal, new Action<String>(showOnTBMSG2), "開啟" + words[0] + "失敗.");
				}));
			}
			else {
				Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
					Dispatcher.Invoke(DispatcherPriority.Normal, new Action<String>(showOnTBMSG1), "");
					Dispatcher.Invoke(DispatcherPriority.Normal, new Action<String>(showOnTBMSG2), "偵測到多個RFID Reader.");
					this.ComboBoxReader.IsEnabled = true;
					this.ButtonConnect.IsEnabled = true;
				}));
			}

			
		}

		private void DoConnectWork() {
			Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
				string[] words = null;
				showOnTBMSG1("正在嘗試連接RFID Reader...");
				showOnTBMSG2("");
				Thread.Sleep(100);
				try {
					words = (ComboBoxReader.SelectedItem as string).Split(' ');
					showOnTBMSG2(string.Format("連接{0}中...", words[0]));
					Thread.Sleep(100);
					this.ReaderService = new ReaderService();
					this.ReaderService.Open(words[0], 38400, (Parity)Enum.Parse(typeof(Parity), "None", true), 8, (StopBits)Enum.Parse(typeof(StopBits), "One", true));
					
					showOnTBMSG1("");
					if (this.ReaderService.IsOpen()) {
						showOnTBMSG2(string.Format("{0}已連接", words[0]));
						this.ButtonEnter.IsEnabled = true;
						this.ButtonConnect.IsEnabled = false;
					}
					else
						showOnTBMSG2("開啟" + words[0] + "失敗.");
				}
				catch (System.Exception ex) {
					showOnTBMSG1("嘗試連接失敗");
					showOnTBMSG2(ex.Message);
				}
				
			}));	
		}

		private void showOnTBMSG1(String str) { this.TBMSG1.Text = str; }
		private void showOnTBMSG2(String str) { this.TBMSG2.Text = str; }
		private void collectionData(ObservableCollection<string> s) {
			this.ComboBoxReader.ItemsSource = s;
			this.ComboBoxReader.SelectedIndex = 0;
		}
		private void OnComboboxReaderDropDownOpened(object sender, EventArgs e) {
			ObservableCollection<string> oc = new ObservableCollection<string>();
			foreach (ReaderService.ReaderSearch rs in ReaderService.ReaderSearch.GetReader())
            {
				if (rs != null) {
					this.ReaderService = new ReaderService();
                    Dispatcher.Invoke(DispatcherPriority.Normal, new Action<String>(showOnTBMSG2), string.Format("驗證{0}", rs.Name));
                    this.ReaderService.Open(rs.Name, 38400, (Parity)Enum.Parse(typeof(Parity), "None", true), 8, (StopBits)Enum.Parse(typeof(StopBits), "One", true));
					this.ReaderService.Send(this.ReaderService.Command_V());
					byte[] b = this.ReaderService.Receive(100);
					if (b != null) {
						string s = this.ReaderService.RemoveCRLF(this.ReaderService.BytesToString(b));
						if (s.Contains("V"))
							oc.Add(string.Format("{0} –{1}[{2}]", rs.Name, rs.Description, s));
					}
					this.ReaderService.Close();
					this.ReaderService = null;
				}
			}
			if (oc.Count > 0)
            {
				this.ComboBoxReader.ItemsSource = oc;
				this.ComboBoxReader.SelectedIndex = 0;
				this.ButtonConnect.IsEnabled = true;
			}
			
		}
		private void OnButtonConnectClick(object sender, RoutedEventArgs e)
        {
			this.SearchThread = new Thread(DoConnectWork);
			this.SearchThread.IsBackground = true;
			this.SearchThread.Start();
		}
		private void OnButtonEnterClick(object sender, RoutedEventArgs e) {
			this.DialogResult = true;
		}
		private void onConnectDialogCloseClick(object sender, RoutedEventArgs e)
        {
			this.DialogResult = false;
			if (this.ReaderService != null && this.ReaderService.IsOpen())
				this.ReaderService.Close();
			this.Close();
		}
		private void OnConnectBorderMouseLeftDown(object sender, MouseButtonEventArgs e)
        {
			this.DragMove();
		}

		public ReaderService GetService() { return this.ReaderService; }
		
	}
}
