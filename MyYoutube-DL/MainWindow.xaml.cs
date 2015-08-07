using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows.Threading;
using System.ComponentModel;
using System.IO;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using RadioButton = System.Windows.Controls.RadioButton;


namespace MyYoutube_DL
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public byte Tempbyte;

        private string _arguments;

        private string _data;

        public bool IsReadyToShow = false;

        private const string Path = "youtube-dl.exe";

        private Thread _myThread;

        public List<RadioButton> buttons;

        public object Locker = new object();

        private TaskManager _taskManager;


        public List<Video> Videoslist;

        // Объект типа АКНО
        public static MainWindow Wm;
        

        public MainWindow()
        {
            InitializeComponent();

            //создание статической ссылки на объект MainWindow 
            Wm = this;
            _taskManager = new TaskManager();

        }


        //обработчик метода вызванного из списка заданий
        public void InMainDispatch(Action dlg)
        {
            if (Thread.CurrentThread.Name == "Main Thread") 
                dlg();
            else
            {
                Wm.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<string>(delegate { dlg(); }), "?");
            }
        }

        /// <summary>запуск приложения | путь к файлу, [аргументы], [ожидать ли окончания] </summary>
        private void Start(string filename, string arguments = "", bool waitforexit = false)
        {
            //создаём класс запускаемого процесса
            var myProc = new Process
            {
                StartInfo =
                {
                    //сообщаем классу процесса путь к исполняемому приложению
                    FileName = filename,
                    Arguments = arguments,
                    //не показывать окно приложения 
                    CreateNoWindow = true,
                    //перенапрвляем текст
                    RedirectStandardOutput = true,
                    
                    //запуск самого процесса без выбора способа открытия ОС
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };


            //прописываем обработчик события начала вывода из процесса для асинхронного считывания
            myProc.OutputDataReceived += (sender, args) => Display(args.Data);

            //запуск процесса
            myProc.Start();

            //начило асинхронного считывания
            myProc.BeginOutputReadLine();

            //при необходимости ждём пока не завершится процесс
           // if (waitforexit)  myProc.WaitForExit();
        }

        /// <summary> метод вызываемый обработчиком/nвыводит строку в текстовый блок </summary>
        void Display(string output)
        {
            _data += output + "\n";
            if (_taskManager._actList.Count > 0)
            {
                if (Tempbyte < _taskManager._actList.Count) Tempbyte = (byte)_taskManager._actList.Count;
            }
            Dispatcher.Invoke(new Action(() => TextOutput.Text += Tempbyte + " || " + _taskManager._actList.Count+"\t" + output + "\n")); 
        }


        /// <summary> Метод проверки ссылки на адекватность. Возвращает булевое значение</summary>
        private bool Validlink(string link)
        {
            if (link.Contains("http://www.youtube.com/watch?v=")||link.Contains("http://youtu.be/")) return true;
            MessageBox.Show("Ссылка неверная");
            return false;
        }


        /// <summary> сортировка списка типа  Video по  формату </summary>
        public void SortVideoListByFormat(List<Video> videoslist, out List<Video> sortedvideoslist)
        {
            //сортируем список видео
            if (videoslist != null)
            {
                var query = videoslist.OrderBy(o => o.Format).ThenByDescending(o => o.Resolution);

               //конвертируем результат LINQ сортировки обратно в список
                sortedvideoslist = new List<Video>(query);

                videoslist.Clear();

                IsReadyToShow = true;
            }
           
           sortedvideoslist = null;
        }

        public void GenerateVideoList(string data, out List<Video> videoslist)
        {
            //формируем список строк - форматов видео
            var formatslist = data.Split('\n').ToList();
            
            //очищаем список от пустот и лишних строк
            for (var i = 0; i <= formatslist.ToArray().Length - 1; i++)
            {
                var s = formatslist[i];

                if (s != "" && Char.IsDigit(s[0])) continue;
                formatslist.RemoveAt(i);
                i--;
            }


            //создаём списки параметров и экземпляры видео
            var numbers = new List<int>();
            var formats = new List<string>();
            var resolutions = new List<string>();

            videoslist = new List<Video>();


            //разделяем строку по столбцам
            foreach (var i in formatslist)
            {
                var tempstringlist = i.Split('\t');

                //отделяем число от строки и создаём список номеров
                int number;
                int.TryParse(tempstringlist[0].Trim(), out number);
                numbers.Add(number);

                //отделяем формат от строки и создаём список форматов
                var tempformat = tempstringlist[2].Trim();
                formats.Add(tempformat);

                //отделяем разрешение от строки и создаём список разрешений
                resolutions.Add(tempstringlist[3].Replace("[", "").Replace("]", "").Trim());
            }

            //заполняем список видео экзеплярами
            for (var i = 0; i <= numbers.Count - 1; i++)
            {
                videoslist.Add(new Video(numbers[i], resolutions[i], formats[i]));
            }
        }




        private void ParsingFormats()
        {
            //сортируем полученный список видео
            SortVideoListByFormat(Videoslist, out Videoslist);

            //================================

            //выполняем в диспетчере  делегат создания группы "choses" радиобатонов
            //с именами состоящими  из разрешения и формата видео
            ButtonsNest.Children.Clear();
            //инициализируем список кнопок
            buttons = new List<RadioButton>();
            //имплементируем кнопки
            for (var i = 0; i <= Videoslist.Count - 1; i++)
            {
                var b = new RadioButton
                {
                    //присваием имя и группу
                    Content = Videoslist[i].Resolution + "   \t" + Videoslist[i].Format,
                    Tag = i,
                    GroupName = "choses"
                };
                //присваемваем созданным кнопкам обработчик состоянияTextOutput.Visibility
                b.Checked += RadioCheked;
                //добавляем сформированные кнопки в список
                buttons.Add(b);
                //добавляем полученные кнопки в ButtonsNest
                ButtonsNest.Children.Add(buttons[i]);
            }
        }


        //private void ParsingFormats()
        //{
        //    //обрабатываем в отдельном потоке
        //    _myThread = new Thread(delegate()
        //    {
        //        //создаём вечный цикл 
        //        while (true)
        //        {
        //            //если не запущен, то есть завершён, запускаем процесс создания списка объектов типа Video
        //            if (!IsProcessRunning())
        //            {
        //                //включаем кнопку
        //                Dispatcher.Invoke(new Action(() => ButtonGetTypes.IsEnabled = true));
        //                //формируем список строк - форматов видео
        //                var formatslist = data.Split('\n').ToList();

        //                //стираем переменную вывода
        //                data = null;

        //                //очищаем список от пустот и лишних строк
        //                for (var i = 0; i <= formatslist.ToArray().Length - 1; i++)
        //                {
        //                    var s = formatslist[i];

        //                    if (s != "" && Char.IsDigit(s[0])) continue;
        //                    formatslist.RemoveAt(i);
        //                    i--;
        //                }


        //                //создаём списки параметров и экземпляры видео
        //                var numbers = new List<int>();
        //                var formats = new List<string>();
        //                var resolutions = new List<string>();

        //                Videoslist = new List<Video>();


        //                //разделяем строку по столбцам
        //                foreach (var i in formatslist)
        //                {
        //                    var tempstringlist = i.Split('\t');

        //                    //отделяем число от строки и создаём список номеров
        //                    var tempnumber = tempstringlist[0].Trim();
        //                    int number;
        //                    int.TryParse(tempnumber, out number);
        //                    numbers.Add(number);

        //                    //отделяем формат от строки и создаём список форматов
        //                    var tempformat = tempstringlist[2].Trim();
        //                    formats.Add(tempformat);

        //                    //отделяем разрешение от строки и создаём список разрешений
        //                    var tempresolution = tempstringlist[3].Replace("[", "").Replace("]", "").Trim();
        //                    resolutions.Add(tempresolution);
        //                }

        //                //заполняем список видео экзеплярами
        //                for (var i = 0; i <= numbers.Count - 1; i++)
        //                {
        //                    Videoslist.Add(new Video(numbers[i], resolutions[i], formats[i]));
        //                }

        //                //сортируем список видео
        //                var query =
        //                    Videoslist.OrderBy(o => o.Format).ThenByDescending(o => o.Resolution);

        //                //конвертируем результат LINQ сортировки обратно в список
        //                var q = new List<Video>(query);

        //                //Очищаем ненужные массивы/списки
        //                Videoslist.Clear();
        //                //заполняем список видео новыми сортированными данными
        //                foreach (var v in q)
        //                {
        //                    Videoslist.Add(v);
        //                }
        //                //очищаем временный список
        //                q.Clear();

        //                //ставим флаг готовности для вывода
        //                IsReadyToShow = true;

        //                //================================


        //                //выполняем в диспетчере  делегат создания группы "choses" радиобатонов
        //                //с именами состоящими  из разрешения и формата видео
        //                Wm.Dispatcher.Invoke(new Action(() =>
        //                {
        //                    ButtonsNest.Children.Clear();
        //                    //инициализируем список кнопок
        //                    buttons = new List<RadioButton>();
        //                    //имплементируем кнопки
        //                    for (var i = 0; i <= Videoslist.Count - 1; i++)
        //                    {
        //                        var b = new RadioButton
        //                        {
        //                            //присваием имя и группу
        //                            Content = Videoslist[i].Resolution + "   \t" + Videoslist[i].Format,
        //                            Tag = i,
        //                            GroupName = "choses"
        //                        };
        //                        //присваемваем созданным кнопкам обработчик состояния
        //                        b.Checked += RadioCheked;
        //                        //добавляем сформированные кнопки в список
        //                        buttons.Add(b);
        //                        //добавляем полученные кнопки в ButtonsNest
        //                        ButtonsNest.Children.Add(buttons[i]);
        //                    }
        //                }
        //                    ));
        //                return;
        //            }
        //            //если выполняется ждём 
        //            Thread.Sleep(200);
        //        }
        //    }
        //        //установка свойств потока
        //        ) {Name = "mythread", IsBackground = true};

        //    //запуск потока
        //    _myThread.Start();
        //}

        // ============================================= XAML методы =================================================


        /// <summary> обработчик нажатия на кнопку скачивания видео с максимальным разрешением </summary>
        public void ButtonMaxResolution_Click(object sender, RoutedEventArgs e)
        {
            //включаем текстовый блок вывода
            //TextOutput.Visibility = Visibility.Visible;
            
            _taskManager.Add(() => Wm.Dispatcher.Invoke(new Action(() =>
            {

                //выходим, если ссылка неадекватна
                if (!Validlink(TextBoxLink.Text)) return;
                //задаём аргумент для скачивания максимального разрешения
                _arguments = " --max-quality -f " + TextBoxLink.Text;

                Start(Path, _arguments);

            })));
            

            
        }

        //обработчик изменения текста вывода, просто скролит вниз
        private void LabelOutput_TextChanged(object sender, TextChangedEventArgs e)
        {
        	TextOutput.ScrollToEnd();
        }

        //проверяет запущен ли процесс
        private static bool IsProcessRunning()
        {
            foreach (var i in Process.GetProcesses())
            {
                if (i.ProcessName != "youtube-dl") continue;

                //Dispatcher.Invoke(new Action(() => TextOutput.Text += "Уже запущено\n"));
                //Thread.Sleep(200);                            
                return true;
            }
            return false;
        }

        //обработчик нажатия на кнопку получения информации с сайта
        public void ButtonGetTypes_Click(object sender, RoutedEventArgs e)
        {
            //проверка не запущен ли процесс
            if (IsProcessRunning()) return;
            
            //проверяем адекватность ссылки
            if (!Validlink(TextBoxLink.Text)) return;

            //формируем аргумент командной строки для получения доступных форматов видео
            _arguments = " -F " + TextBoxLink.Text;

            //очищаем текстовый блок вывода
            TextOutput.Text = null;
            


            //запускаем процесс и ждём пока не завершится
            Start(Path, _arguments,true);
            _taskManager.Add(delegate{

                
                while (IsProcessRunning())
                {
                    Thread.Sleep(20);
                }

            },
                //запускаем метод создания группы радиобатонов из текста полученного от консольного приложения
            ParsingFormats);



            
        }

        
        /// универсальный обработчик для генерируемых радиобатонов
        private void RadioCheked(object sender, RoutedEventArgs e)
        {
            var whichButton = (RadioButton)sender;

            _arguments = " -f " + Videoslist[(int)(whichButton.Tag)].Number + " " + TextBoxLink.Text;
          
            Start(Path, _arguments);
        }
    }
}
