using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BIS_Big_Data__Yeosu__4
{
    class Time
    {
        public int Hour;
        public int Minute;
        public int Second;

        public Time(int Hour, int Minute)
        {
            if (Hour >= 24 && Minute > 0)
                this.Hour = 0;
            else
                this.Hour = Hour;
            this.Minute = Minute;
            this.Second = 0;
        }

        public Time(int Hour, int Minute, int Second)
        {
            if (Hour >= 24 && (Minute > 0 || Second > 0))
                this.Hour = 0;
            else
                this.Hour = Hour;
            this.Minute = Minute;
            this.Second = Second;
        }

        public void FromSeconds(int value)
        {
            TimeSpan time = TimeSpan.FromSeconds(value);
            this.Hour = time.Hours;
            this.Minute = time.Minutes;
            this.Second = time.Seconds;
        }

        public static int TimeClassToSecond(int Hour, int Minute, int Second)
        {
            return Hour * 3600 + Minute * 60 + Second;
        }

        public static int DayStringToSecond(String str)
        {
            DateTime time = DateTime.Parse(str);

            return time.Hour * 3600 + time.Minute * 60 + time.Second;
        }

        public int TimeToSecond()
        {
            return this.Hour * 3600 + this.Minute * 60 + this.Second;
        }

        public static Time operator +(Time L, Time R)
        {
            return new Time(L.Hour + R.Hour, L.Minute + R.Minute, L.Second + R.Second);
        }

        public static Time operator -(Time L, Time R)
        {
            return new Time(L.Hour - R.Hour > 0 ? L.Hour - R.Hour : 0, L.Minute - R.Minute > 0 ? L.Minute - R.Minute : 0, L.Second - R.Second > 0 ? L.Second - R.Second : 0);
        }

        public String toString()
        {
            if (Second > 0)
                return String.Format("{0}:{1}:{2}", Hour, Minute, Second);
            else
                return String.Format("{0}:{1}", Hour, Minute);
        }
        public String toString(String str)
        {
            str = str.Replace("hh", Hour.ToString());
            str = str.Replace("mm", Minute.ToString());
            str = str.Replace("ss", Second.ToString());

            return str;
        }
    }
}
