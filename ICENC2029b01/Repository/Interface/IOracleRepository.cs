using Oracle.ManagedDataAccess.Client;
using ICENC2029b01.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICENC2029b01.Repository.Interface
{
    public interface IOracleRepository
    {

        int ProcessData(ArgDto argDto, ref OracleConnection _oraConn);
    }
}
