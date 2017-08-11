using System;
using System.IO;
using Lykke.Service.FIXQuotes.Core;
using Newtonsoft.Json;
using Xunit;

namespace Lykke.Service.IpGeoLocation.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var js = new JsonSerializer();
            var sw = new StringWriter();
            js.Serialize(sw, new AppSettings());

            Console.Write(sw.ToString());
        }
    }

}
