using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterHttpClient.Socks
{
    public class NeverEndingStream : MemoryStream
    {
        public override void Close()
        {
            // Ignore
        }

        public void ForceClose()
        {
            base.Close();
        }
    }
}
