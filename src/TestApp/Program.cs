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
        static void Main(string[] args)
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
                Thread.Sleep(1000);
            }
            Console.WriteLine();
            Console.WriteLine("Done...");
        }
    }
}