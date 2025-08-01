using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICENC2029b01.Models
{
    public class ArgDto
    {
        public string? _sExecFlag { get; set; }
        public string? _sInputFeeYm { get; set; }
        public string? _sInputHospId { get; set; }
        public string? _sValidSDate { get; set; }
        public string? _sMark24hr { get; set; }
        public string? _wkBranchCode { get; set; }




        public string? _chkDate { get; set; }

        //參數數量
        public int? _count { get; set; }

    }

    public class ProList
    {
        public int exitCode = -999;
        public string message = string.Empty;
    }
}
