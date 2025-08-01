using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICENC2029b01.Services.Interface
{
    public interface IMainService
    {
        int RunBatchJob(string[] args);
        int CheckArg(string[] args);
        
    }
}
