using System;
using System.IO;

namespace Producer.Services
{
    public static class Addresses
    {
        private static readonly string[] Keys;
        private static readonly Random Rnd;

        static Addresses()
        {
            Keys = LoadKeys();
            Rnd = new Random();
        }

        public static string GetAddress()
        {
            return Keys[Rnd.Next(Keys.Length)];
        }

        private static string[] LoadKeys()
        {
            var path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            return File.ReadAllLines(path + "/addresses.csv");
        }
    }
}
