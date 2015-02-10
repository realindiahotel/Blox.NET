using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Bitcoin.Lego;
using Bitcoin.BitcoinUtilities;

namespace TestUI
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		private void button_Click(object sender, RoutedEventArgs e)
		{
			P2PConnection p2p = new P2PConnection(System.Net.IPAddress.Parse("108.61.123.187"), 60000);
            bool success = p2p.Connect(Globals.NodeNetwork,1,Globals.RelayTransactionsAlways,true);

			if (!success)
			{
				MessageBox.Show("Not connected");
			}


		}
	}
}
