using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Windows.Forms;
using System.ComponentModel;

namespace ProcessControl
{
    public class ProcessConfigList
    {
        public ObservableCollection<ProcessConfig> List { get; set; } = new ObservableCollection<ProcessConfig>();

        public void Sort(ProcessSort sort, bool reverse = false)
        {
            List<ProcessConfig> sorted = List.ToList();

            switch (sort)
            {
                case ProcessSort.Name:
                    sorted.Sort(delegate (ProcessConfig c1, ProcessConfig c2) { return c1.ProcessName.CompareTo(c2.ProcessName); });
                    break;
                case ProcessSort.Priority:
                    sorted.Sort(delegate (ProcessConfig c1, ProcessConfig c2) { return c1.Priority.CompareTo(c2.Priority); });
                    break;
                case ProcessSort.System:
                    sorted.Sort(delegate (ProcessConfig c1, ProcessConfig c2) { return c1.System.CompareTo(c2.System); });
                    break;
            }

            List.Clear();

            if (reverse)
                sorted.Reverse();

            foreach (var v in sorted)
                List.Add(v);
        }
    }

    public class View
    {
        public ProcessConfigList ProcessConfigIgnored { get; set; } = new ProcessConfigList();
        public ProcessConfigList ProcessConfig { get; set; } = new ProcessConfigList();

        public View()
        {
            ProcessConfig = new ProcessConfigList()
            {
                List = {

                }
            };
        }
    }

    public partial class MainWindow : Window
    {
        private View view = new View();

        public static ObservableCollection<ProcessConfig> applyed = new ObservableCollection<ProcessConfig>();

        private Task thread = null;
        private static Mutex mutex = new Mutex();

        private WinForms.NotifyIcon notifier = new WinForms.NotifyIcon();

        void notifier_MouseDown(object sender, WinForms.MouseEventArgs e)
        {
            if (e.Button == WinForms.MouseButtons.Right)
            {
                System.Windows.Controls.ContextMenu menu = (System.Windows.Controls.ContextMenu)this.FindResource("NotifierContextMenu");
                menu.IsOpen = true;
            }
        }

        public MainWindow()
        {
            ShowInTaskbar = true;
            InitializeComponent();

            JArray array = JsonConvert.DeserializeObject<JArray>(File.ReadAllText("data.json"));
            foreach (var v in array) {
                ProcessConfig config = new ProcessConfig(
                    v["Name"].ToString(),
                    ConvertToConfig(v["Priority"].ToString()),
                    v["System"].ToString() == "True" ? true : false
                );
                view.ProcessConfig.List.Add(config);
                applyed = Copy(view.ProcessConfig.List);
            }

            this.notifier.MouseDown += new WinForms.MouseEventHandler(notifier_MouseDown);
            this.notifier.Icon = new System.Drawing.Icon("icon.ico");
            this.notifier.Visible = true;

            DataContext = view;

            PrioritySelector.Items.Add("Close");
            PrioritySelector.Items.Add("VeryLow");
            PrioritySelector.Items.Add("Low");
            PrioritySelector.Items.Add("Normal");
            PrioritySelector.Items.Add("High");
            PrioritySelector.Items.Add("VeryHigh");
            PrioritySelector.Items.Add("RealTime");

            thread = Task.Factory.StartNew(() => {
                while (true)
                {
                    Thread.Sleep(10000);
                    mutex.WaitOne();

                    foreach (var v in Process.GetProcesses())
                        foreach (var conf in applyed)
                            if (v.ProcessName == conf.ProcessName)
                            {
                                if (conf.Priority == Config.Close)
                                {
                                    v.Kill();
                                    break;
                                }

                                if (conf.Priority == Config.Unknown)
                                    break;

                                v.PriorityClass = ConvertToPriority(conf.Priority);

                                break;
                            }

                    mutex.ReleaseMutex();
                }
            });
        }

        private bool FindProcess(ProcessConfigList list, string name)
        {
            foreach (var v in list.List)
                if (v.ProcessName == name)
                    return true;
            return false;
        }

        public static ProcessPriorityClass ConvertToPriority(Config config)
        {
            switch (config)
            {
                case Config.VeryLow:
                    return ProcessPriorityClass.Idle;
                case Config.Low:
                    return ProcessPriorityClass.BelowNormal;
                case Config.Normal:
                    return ProcessPriorityClass.Normal;
                case Config.High:
                    return ProcessPriorityClass.AboveNormal;
                case Config.VeryHigh:
                    return ProcessPriorityClass.High;
                case Config.RealTime:
                    return ProcessPriorityClass.RealTime;
            }
            return ProcessPriorityClass.Idle;
        }

        private Config ConvertToConfig(string priorityClass)
        {
            switch (priorityClass)
            {
                case "Unknown":
                    return Config.Unknown;
                case "Normal":
                    return Config.Normal;
                case "VeryLow":
                    return Config.VeryLow;
                case "VeryHigh":
                    return Config.VeryHigh;
                case "RealTime":
                    return Config.RealTime;
                case "Low":
                    return Config.Low;
                case "High":
                    return Config.High;
            }
            return Config.Close;
        }

        private Config ConvertToConfig(ProcessPriorityClass priorityClass)
        {
            switch (priorityClass)
            {
                case ProcessPriorityClass.Normal:
                    return Config.Normal;
                case ProcessPriorityClass.Idle:
                    return Config.VeryLow;
                case ProcessPriorityClass.High:
                    return Config.VeryHigh;
                case ProcessPriorityClass.RealTime:
                    return Config.RealTime;
                case ProcessPriorityClass.BelowNormal:
                    return Config.Low;
                case ProcessPriorityClass.AboveNormal:
                    return Config.High;
            }
            return Config.Close;
        }

        private void ProcessConfigIgnored_MouseDoubleClick(object sender, RoutedEventArgs e) {
            var item = ((FrameworkElement)e.OriginalSource).DataContext as ProcessConfig;
            if (item != null)
            {
                if (item.Priority != Config.Unknown)
                    view.ProcessConfig.List.Add(item);
                Button_Click(null, null);
            }
        }

        private void ProcessConfig_MouseDoubleClick(object sender, RoutedEventArgs e)
        {
            var item = ((FrameworkElement)e.OriginalSource).DataContext as ProcessConfig;
            if (item != null)
            {
                mutex.WaitOne();
                    view.ProcessConfig.List.Remove(item);
                mutex.ReleaseMutex();

                Button_Click(null, null);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            mutex.WaitOne();
            view.ProcessConfigIgnored.List.Clear();

            foreach (Process process in Process.GetProcesses())
            {
                if (!FindProcess(view.ProcessConfigIgnored, process.ProcessName))
                    if (!FindProcess(view.ProcessConfig, process.ProcessName))
                    {
                        try
                        {
                            ProcessPriorityClass priorityClass = process.PriorityClass;

                            view.ProcessConfigIgnored.List.Add(
                                new ProcessConfig(
                                        process.ProcessName,
                                        ConvertToConfig(priorityClass),
                                        false
                                    )
                                );
                        }
                        catch (System.ComponentModel.Win32Exception ex)
                        {
                            view.ProcessConfigIgnored.List.Add(
                                new ProcessConfig(
                                        process.ProcessName,
                                        Config.Unknown,
                                        true
                                    )
                                );
                        }
                    }
            }

            mutex.ReleaseMutex();
        }

        private void ProcessConfigList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ProcessConfig process = (ProcessConfig)AllProcess.SelectedItem;
            if (process != null)
            {
                //   System.Windows.MessageBox.Show(process.ProcessName);
                PrioritySelector.SelectedItem = process.Priority.ToString();
                //MessageBox.Show(process.Priority.ToString());
            }
        }

        ObservableCollection<ProcessConfig> Copy(ObservableCollection<ProcessConfig> source)
        {
            ObservableCollection<ProcessConfig> result = new ObservableCollection<ProcessConfig>();
            foreach (var v in source)
                result.Add(v);
            return result;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            mutex.WaitOne();

            ProcessConfig process = (ProcessConfig)AllProcess.SelectedItem;

            ObservableCollection<ProcessConfig> List = Copy(view.ProcessConfig.List);

            view.ProcessConfig.List.Clear();

            if (process != null)
            {
                //if (ConvertToConfig((string)PrioritySelector.SelectedItem) == Config.Close)
                    process.Priority = ConvertToConfig((string)PrioritySelector.SelectedItem);
            }

            foreach (var v in List)
                view.ProcessConfig.List.Add(v);

            mutex.ReleaseMutex();
        }

        GridViewColumnHeader ProcessesColumn_lastHeaderClicked = null;
        ListSortDirection ProcessesColumn_lastDirection = ListSortDirection.Ascending;

        GridViewColumnHeader IgnoredProcessesColumn_lastHeaderClicked = null;
        ListSortDirection IgnoredProcessesColumn_lastDirection = ListSortDirection.Ascending;

        private void ProcessesColumn_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader headerClicked = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;

            if (headerClicked != null) {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding) {
                    if (headerClicked != ProcessesColumn_lastHeaderClicked)
                        direction = ListSortDirection.Ascending;
                    else
                        if (ProcessesColumn_lastDirection == ListSortDirection.Ascending)
                            direction = ListSortDirection.Descending;
                        else
                            direction = ListSortDirection.Ascending;

                    string header = headerClicked.Column.Header as string;
                    if (header == "ProcessName")
                        view.ProcessConfig.Sort(ProcessSort.Name,
                            direction == ListSortDirection.Descending ? true : false);
                    else if (header == "Priority")
                        view.ProcessConfig.Sort(ProcessSort.Priority,
                            direction == ListSortDirection.Descending ? true : false);
                    else if (header == "Is system")
                        view.ProcessConfig.Sort(ProcessSort.System,
                            direction == ListSortDirection.Descending ? true : false);

                    // Remove arrow from previously sorted header
                    if (ProcessesColumn_lastHeaderClicked != null && ProcessesColumn_lastHeaderClicked != headerClicked)
                        ProcessesColumn_lastHeaderClicked.Column.HeaderTemplate = null;

                    ProcessesColumn_lastHeaderClicked = headerClicked;
                    ProcessesColumn_lastDirection = direction;
                }
            }
        }
        private void IgnoredProcessesColumn_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader headerClicked = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;

            if (headerClicked != null)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
                    if (headerClicked != IgnoredProcessesColumn_lastHeaderClicked)
                        direction = ListSortDirection.Ascending;
                    else
                        if (IgnoredProcessesColumn_lastDirection == ListSortDirection.Ascending)
                        direction = ListSortDirection.Descending;
                    else
                        direction = ListSortDirection.Ascending;

                    //if (direction == ListSortDirection.Ascending)
                    //    System.Windows.MessageBox.Show("1");

                    string header = headerClicked.Column.Header as string;
                    if (header == "ProcessName")
                        view.ProcessConfigIgnored.Sort(ProcessSort.Name,
                            direction == ListSortDirection.Descending ? true : false);
                    else if (header == "Priority")
                        view.ProcessConfigIgnored.Sort(ProcessSort.Priority,
                            direction == ListSortDirection.Descending ? true : false);
                    else if (header == "Is system")
                        view.ProcessConfigIgnored.Sort(ProcessSort.System,
                            direction == ListSortDirection.Descending ? true : false);

                    // Remove arrow from previously sorted header
                    if (IgnoredProcessesColumn_lastHeaderClicked != null && IgnoredProcessesColumn_lastHeaderClicked != headerClicked)
                        IgnoredProcessesColumn_lastHeaderClicked.Column.HeaderTemplate = null;

                    IgnoredProcessesColumn_lastHeaderClicked = headerClicked;
                    IgnoredProcessesColumn_lastDirection = direction;
                }
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            applyed = Copy(view.ProcessConfig.List);

            JArray array = new JArray();

            foreach (var v in applyed)
            {
                JObject o = new JObject();
                o["Name"] = v.ProcessName;
                o["Priority"] = v.Priority.ToString();
                o["System"] = v.System;

                array.Add(o);
            }

            string json = JsonConvert.SerializeObject(array);
            File.WriteAllText("data.json", json);
        }

        private void Menu_Open(object sender, RoutedEventArgs e)
        {
            this.Show();
        }

        private void Menu_Close(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }
    }
}

