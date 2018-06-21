using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ClientRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            var hx = new HttpClient();
            var httpRequest = new HttpRequestMessage(
                HttpMethod.Get, "http://www.vocm.com/");
            var res = hx.SendAsync(httpRequest).GetAwaiter().GetResult();
        }
    }
}
