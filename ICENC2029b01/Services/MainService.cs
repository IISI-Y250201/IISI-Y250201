using ICENC2029b01.Exceptions;
using ICENC2029b01.Models;
//using ENVCC0001N01;
//using ICMCC0001N01;
using NLog;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using ICENC2029b01.Repository;
using StackExchange.Redis;
using ICENC2029b01.Services.Interface;
using System.Diagnostics;
using ZstdSharp.Unsafe;
using System.Security.Cryptography;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Med3Core.CB.ENV.Common;
using System.Collections;
using static MongoDB.Driver.WriteConcern;

namespace ICENC2029b01.Services
{
    public class MainService : IMainService
    {
        private OracleConnection _oraConn = new OracleConnection(ENV_GetDBInfo.GetHmDBConnectString); //Oracle 取得連線
        private Logger _logger = LogManager.GetLogger(AppDomain.CurrentDomain.FriendlyName.Substring(0, 3).ToUpper());
        private OracleRepository _oracleRepository = new OracleRepository();
        private readonly ActivitySource _activitySource = new ActivitySource("OpenTelemetrySource");
        private class ProList
        {
            public int exitCode = -999;
            public string message = string.Empty;
        }

        /* 欄位內容 */
        private ArgDto argDto = new ArgDto();
        private string _exeBranchCode = string.Empty;
        private int ExitCode = 0;

        public int RunBatchJob(string[] args)
        {
            try
            {
                using var activity = _activitySource.StartActivity("RunBatchJob", ActivityKind.Internal);
                using (ScopeContext.PushProperty("FunCode", "RunBatchJob"))
                {
                    _oraConn.Open(); // Oralce 開啟與資料庫連線
                    _oraConn.BeginTransaction();

                    //_exeBranchCode = EnvVariables.BRANCH_CODE; /* 執行環境分區別 */
                    _exeBranchCode = Med3Core.CB.ENV.Common.ENV_EnvVariables.DEFAULT_BRANCH_CODE;

                    Console.WriteLine("========== ICENC2029b01 start ========== ");

                    int rtn = CheckArg(args);
                    if (rtn == 0)
                    {
                        _oracleRepository.ProcessData(argDto, ref _oraConn);
                    }

                    if (_oraConn.State == ConnectionState.Open)
                    {
                        _oraConn.Commit();
                    }

                    Console.WriteLine("========== icei2029b01 end ========== ");

                    ExitCode = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                _oraConn.Rollback();
                ExitCode = -1;
            }
            finally
            {
                _oraConn.Close(); // Oracle 斷開連線
                _oraConn.Dispose(); // Oracle 回收物件
                OracleConnection.ClearPool(_oraConn);
            }
            return ExitCode;
        }

        /// <summary>
        /// 檢查本程式所需之參數是否符合規定
        /// </summary>
        /// <param name="args">傳入程式之參數</param>
        public int CheckArg(string[] args)
        {
            string paraMsg = string.Empty;
            try
            {
                if (args.Length < 1 || args.Length == 0)
                {
                    ShowUsage();
                    Console.WriteLine("參數個數不符");
                    return -1;
                }

                argDto._sExecFlag = args[0];
                Console.WriteLine($" s_exec_flag:[{argDto._sExecFlag}] argc:[{args.Length}] ");

                if (args.Length >= 2)
                {
                    argDto._sInputFeeYm = args[1];
                    Console.WriteLine($"    s_input_fee_ym:[{argDto._sInputFeeYm}] ");
                }

                if (args.Length >= 3)
                {
                    argDto._sInputHospId = args[2];
                    Console.WriteLine($"    s_input_hosp_id:[{argDto._sInputHospId}] ");
                }

                if (args.Length >= 4)
                {
                    argDto._sValidSDate = args[3];
                    Console.WriteLine($"    s_valid_s_date:[{argDto._sValidSDate}] ");
                }

                if (args.Length >= 5)
                {
                    argDto._sMark24hr = args[4];
                    Console.WriteLine($"    s_mark_24hr:[{argDto._sMark24hr}] ");
                }

                if (args.Length >= 6)
                {
                    argDto._wkBranchCode = args[5];
                    Console.WriteLine($"    wk_branch_code:[{argDto._wkBranchCode}] ");
                }

                // Validate arguments
                if ((argDto._sExecFlag == "0" || argDto._sExecFlag == "1") && (args.Length <= 1 || args.Length >= 4))
                {
                    ShowUsage();
                    Console.WriteLine("參數個數不符");
                    return -1;
                }

                if (argDto._sExecFlag == "2" && (args.Length != 5 && args.Length != 6))
                {
                    ShowUsage();
                    Console.WriteLine("參數個數不符");
                    return -1;
                }

                if (!(argDto._sExecFlag == "0" || argDto._sExecFlag == "1" || argDto._sExecFlag == "2"))
                {
                    ShowUsage();
                    Console.WriteLine("執行類別參數錯誤");
                    return -1;
                }

                if (args.Length == 6 &&
                    !(argDto._wkBranchCode == "1" || argDto._wkBranchCode == "2" || argDto._wkBranchCode == "3" ||
                      argDto._wkBranchCode == "4" || argDto._wkBranchCode == "5" || argDto._wkBranchCode == "6"))
                {
                    ShowUsage();
                    Console.WriteLine("分區別參數錯誤");
                    return -1;
                }
                return 0;
            }
            catch
            {
                Console.WriteLine(paraMsg);
                _logger.Error(paraMsg);
                return -1;
            }
        }

        private void ShowUsage()
        {
            var prog = AppDomain.CurrentDomain.FriendlyName;
            Console.WriteLine("執行類別： 0 - update MARK_24HR = 8 ( 24小時上傳註記: 8-例外院所 ) ");
            Console.WriteLine("           1 - update mark_560      ( Y : 醫令代碼 為 重要檢驗（查）結果之項目 ) ");
            Console.WriteLine(" ");
            Console.WriteLine("參數種類： 程式代號  執行類別  費用年月  醫事機構代碼(選項)    ");
            Console.WriteLine(" ");
            Console.WriteLine($"範例1   ： {prog}  0  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 ");
            Console.WriteLine($"範例2   ： {prog}  0  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 ");
            Console.WriteLine($"範例3   ： {prog}  0  20190101  [all]        *參數給all，全部院所重新執行。  ");
            Console.WriteLine(" ");
            Console.WriteLine("參數種類： 程式代號  執行類別  費用年月  醫事機構代碼(選項)    ");
            Console.WriteLine(" ");
            Console.WriteLine($"範例4   ： {prog}  1  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 ");
            Console.WriteLine($"範例5   ： {prog}  1  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 ");
            Console.WriteLine($"範例6   ： {prog}  1  20190101  [all]        *參數給all，全部院所重新執行。  ");
            Console.WriteLine("參數種類： 程式代號  執行類別  費用年月  醫事機構代碼(選項)  有效迄日 更新註記  分區別(選項)  ");
            Console.WriteLine(" ");
            Console.WriteLine($"範例7   ： {prog}  2  20190101  \"\"       20190101  A     *未給醫事機構代碼，只做尚未計算給付上限的院所。 ");
            Console.WriteLine($"範例8   ： {prog}  2  20190101  [hosp_id]  20190101  A     *提供醫事機構代碼，只做該院所。 ");
            Console.WriteLine($"範例9   ： {prog}  2  20190101  [all]      20190101  A  2  *參數給all，全部院所重新執行。  ");
        }
    }
}
