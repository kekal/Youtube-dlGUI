using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MyYoutube_DL
{
    public class Video
    {
        public int Number;
        public string Resolution, Format;

        /// конструктор задаёт имя нового аргумента командной строки (при создании экземпляра, очевидно)
        public Video(int number, string resolution, string format)
        {
            Number = number;
            Resolution = resolution;
            Format = format;
        }

    }
}
