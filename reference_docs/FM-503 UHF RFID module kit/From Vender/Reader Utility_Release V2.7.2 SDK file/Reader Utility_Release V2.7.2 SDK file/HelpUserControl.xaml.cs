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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ReaderUtility
{
	/// <summary>
	/// Interaction logic for HelpUserControl.xaml
	/// </summary>
	public partial class HelpUserControl : UserControl
	{
		public HelpUserControl()
		{
			InitializeComponent();
		}

		public double X
		{
			get { return this.ToolTipXY.X; }
			set { this.ToolTipXY.X = value; }
		}

		public double Y
		{
			get { return this.ToolTipXY.Y; }
			set { this.ToolTipXY.Y = value; }
		}

		public string Data
		{
			get { return TextBlockBitDataHelp.Text; }
			set { TextBlockBitDataHelp.Text = value; }
		}
	}
}
