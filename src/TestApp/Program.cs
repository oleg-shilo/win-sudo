// Ignore Spelling: App

using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestApp
{
    internal class Program
    {
        static int Main(string[] args)
        {
            Task.Run(() =>
            {
                while (true)
                {
                    var input = Console.ReadLine();
                    Console.WriteLine($"> {input.ToUpper()}");
                }
            });

            int count = 0;
            while (count < 7)
            {
                count++;
                Console.Write($"{count}");
                // Console.WriteLine(count);
                Thread.Sleep(500);
            }
            Console.WriteLine();
            Console.WriteLine("Done...");

            return 555;
        }
    }
}