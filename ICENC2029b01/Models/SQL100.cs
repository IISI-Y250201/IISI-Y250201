using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICENC2029b01.Models
{
    public class SQL100
    {
        public string hospId { get; set; } = string.Empty;
        public string recvSDate { get; set; } = string.Empty;
        public string recvEDate { get; set; } = string.Empty;
        public string feeSDate { get; set; } = string.Empty;
        public string feeEDate { get; set; } = string.Empty;
    }
}
