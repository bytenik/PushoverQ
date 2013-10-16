using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ.Tests
{
    class TestLogger : ILog
    {
        public void Trace(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        public void Trace(Exception exception, string format, params object[] args)
        {
            Console.WriteLine(format, args);
            Console.WriteLine(exception.ToString());
        }

        public void Debug(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        public void Debug(Exception exception, string format, params object[] args)
        {
            Console.WriteLine(format, args);
            Console.WriteLine(exception.ToString());
        }

        public void Info(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        public void Info(Exception exception, string format, params object[] args)
        {
            Console.WriteLine(format, args);
            Console.WriteLine(exception.ToString());
        }

        public void Warn(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        public void Warn(Exception exception, string format, params object[] args)
        {
            Console.WriteLine(format, args);
            Console.WriteLine(exception.ToString());
        }

        public void Error(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        public void Error(Exception exception, string format, params object[] args)
        {
            Console.WriteLine(format, args);
            Console.WriteLine(exception.ToString());
        }

        public void Fatal(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        public void Fatal(Exception exception, string format, params object[] args)
        {
            Console.WriteLine(format, args);
            Console.WriteLine(exception.ToString());
        }
    }
}
