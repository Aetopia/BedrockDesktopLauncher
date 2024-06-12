using System;
using System.Windows;


static class Program
{
  [STAThread]
  static void Main()
  {
    new Application().Run(new MainWindow());
  }
}
