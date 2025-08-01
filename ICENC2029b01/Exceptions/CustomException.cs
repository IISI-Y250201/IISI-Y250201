using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICENC2029b01.Exceptions
{
    public class CustomException : Exception
    {
        public int ExitCode { get; }
        public CustomException(string message, int exitCode) : base(message)
        {
            ExitCode = exitCode;
        }
    }
}
