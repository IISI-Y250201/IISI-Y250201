```csharp
/*******************************************************************
程式代碼：icei2029b01
程式名稱：24小時上傳註記勾稽作業
功能簡述：更新24小時上傳註記及重要檢驗結果項目標記
參    數：
參數一：程式代號 執行類別 費用年月 醫事機構代碼(選項) 有效迄日 更新註記 分區別(選項)
範例一：icei2029b01 0 20190101 "" 
範例二：icei2029b01 1 20190101 ALL
範例三：icei2029b01 2 20190101 "" 20190101 A
讀取檔案：無
異動檔案：無
作    者：系統轉換
歷次修改時間：
1.2023/01/01
需求單號暨修改內容簡述：
1.Pro*C轉C#
備    註：
********************************************************************/

using System;
using System.Data;
using System.Text;
using NLog;
using Oracle.ManagedDataAccess.Client;
using NHI.MEDCS.MEDM.Common;

namespace icei2029b01
{
    public class icei2029b01
    {
        #region Static Members
        private static OracleConnection _oraConn = new OracleConnection(GetDBInfo.GetHmDBConnectString);
        private static Logger _logger = LogManager.GetLogger(AppDomain.CurrentDomain.FriendlyName.Substring(0, 3).ToUpper());

        private class ProList
        {
            public int exitCode = -999;
            public string message = string.Empty;
        }
        private static ProList _proList = new ProList();

        // Original Pro*C variables
        private static string _sInputFeeYm = string.Empty;
        private static string _sInputHospId = string.Empty;
        private static string _sValidSDate = string.Empty;
        private static string _sMark24hr = string.Empty;
        private static string _wkBranchCode = string.Empty;
        private static string _sExecFlag = string.Empty;
        private static int _iExeType = 1;
        #endregion

        #region Structs
        private class SQL100
        {
            public string hospId { get; set; } = string.Empty;
            public string recvSDate { get; set; } = string.Empty;
            public string recvEDate { get; set; } = string.Empty;
            public string feeSDate { get; set; } = string.Empty;
            public string feeEDate { get; set; } = string.Empty;
        }
        #endregion

        static void Main(string[] args)
        {
            try
            {
                // PXX_start_process();
                MEDM_SysLib.MEDM_StartProcess();

                Console.WriteLine("========== icei2029b01 start ========== ");

                CheckArg(args);

                if (_sExecFlag == "0")
                {
                    UpdateMark24Hr();
                }

                if (_sExecFlag == "1")
                {
                    UpdateMark560();
                }

                if (_sExecFlag == "2")
                {
                    UpdateMark24hrPBA560();
                }

                Console.WriteLine("========== icei2029b01 end ========== ");

                _proList.exitCode = 0;
                _proList.message = $"{AppDomain.CurrentDomain.FriendlyName} 執行完成";
            }
            catch (OracleException ex)
            {
                _proList.exitCode = 200;
                _proList.message = ex.Message;
                Console.WriteLine(ex.ToString());
                _logger.Error(ex.ToString());
            }
            catch (Exception ex)
            {
                _proList.message = ex.Message;
                Console.WriteLine(ex.ToString());
                _logger.Error(ex.ToString());
            }
            finally
            {
                // PXX_exit_process(rtn_code, 0, msg);
                MEDM_SysLib.MEDM_ExitProcess(_proList.exitCode, _proList.message);
            }
        }

        private static void UpdateMark24Hr()
        {
            int iUpdateCount = 0;
            _iExeType = 1;

            Console.WriteLine($"    0 - UpdateMark24Hr() - select pxxt_code s_exec_flag:[{_sExecFlag}] s_input_hosp_id:[{_sInputHospId}] s_input_fee_ym:[{_sInputFeeYm}] ");

            try
            {
                _oraConn.Open();

                StringBuilder strSQL = new StringBuilder();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("SELECT code hosp_id,");
                    strSQL.AppendLine("       TO_CHAR(VALID_S_DATE,'YYYYMMDD') recv_s_date,");
                    strSQL.AppendLine("       TO_CHAR(VALID_E_DATE,'YYYYMMDD') recv_e_date,");
                    strSQL.AppendLine("       TO_CHAR(VALID_S_DATE,'YYYYMM')||'01' fee_s_date,");
                    strSQL.AppendLine("       TO_CHAR(VALID_E_DATE,'YYYYMM')||'01' fee_e_date");
                    strSQL.AppendLine("  FROM pxxt_code");
                    strSQL.AppendLine(" WHERE sub_sys   = 'ICE'");
                    strSQL.AppendLine("   AND data_type = '109'");
                    strSQL.AppendLine("   AND TRUNC(TO_DATE(:sInputFeeYm,'YYYYMMDD'),'MM') BETWEEN TRUNC(valid_s_date,'MM') AND TRUNC(valid_e_date,'MM')");
                    cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                    strSQL.AppendLine("   AND ( code      = :sInputHospId OR");
                    cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                    strSQL.AppendLine("         'ALL'     = UPPER(:sInputHospIdUpper) OR");
                    cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId));
                    strSQL.AppendLine("         ( NVL(:sInputHospIdNull,0) = 0 AND");
                    cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? "0" : _sInputHospId));
                    strSQL.AppendLine("           exe_time >= ( SELECT EXE_TIME");
                    strSQL.AppendLine("                           FROM pxxt_code");
                    strSQL.AppendLine("                          WHERE sub_sys   = 'ICE'");
                    strSQL.AppendLine("                            AND data_type = '110'");
                    strSQL.AppendLine("                            AND code = '1' ) ) )");

                    cmd.CommandText = strSQL.ToString();

                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SQL100 sql100 = new SQL100
                            {
                                hospId = reader["hosp_id"].ToString(),
                                recvSDate = reader["recv_s_date"].ToString(),
                                recvEDate = reader["recv_e_date"].ToString(),
                                feeSDate = reader["fee_s_date"].ToString(),
                                feeEDate = reader["fee_e_date"].ToString()
                            };

                            StringBuilder updateSQL = new StringBuilder();
                            using (OracleCommand updateCmd = _oraConn.CreateCommand())
                            {
                                updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL a");
                                updateSQL.AppendLine("   SET MARK_24HR = '8'");
                                updateSQL.AppendLine(" WHERE ( a.hosp_id = :hospId OR :hospIdZero = '0000000000' )");
                                updateCmd.Parameters.Add(new OracleParameter("hospId", sql100.hospId));
                                updateCmd.Parameters.Add(new OracleParameter("hospIdZero", sql100.hospId));
                                updateSQL.AppendLine("   AND a.fee_ym BETWEEN TO_DATE(:feeSDate,'YYYYMMDD') AND TO_DATE(:feeEDate,'YYYYMMDD')");
                                updateCmd.Parameters.Add(new OracleParameter("feeSDate", sql100.feeSDate));
                                updateCmd.Parameters.Add(new OracleParameter("feeEDate", sql100.feeEDate));
                                updateSQL.AppendLine("   AND a.CASE_TIME BETWEEN TO_DATE(:recvSDate,'YYYYMMDD') AND TO_DATE(:recvEDate,'YYYYMMDD')");
                                updateCmd.Parameters.Add(new OracleParameter("recvSDate", sql100.recvSDate));
                                updateCmd.Parameters.Add(new OracleParameter("recvEDate", sql100.recvEDate));
                                updateSQL.AppendLine("   AND a.MARK_24HR = '5'");
                                updateSQL.AppendLine("   AND a.recv_D_NUM > 0");

                                updateCmd.CommandText = updateSQL.ToString();
                                int rowsAffected = updateCmd.ExecuteNonQuery();

                                string message = $"    update hosp_id:[{sql100.hospId}] fee_s_date:[{sql100.feeSDate}] fee_e_date:[{sql100.feeEDate}] " +
                                                $"recv_s_date:[{sql100.recvSDate}] recv_e_date:[{sql100.recvEDate}] sqlcode:[0] rec:[{rowsAffected}] ";
                                Console.WriteLine(message);
                                _logger.Info(message);

                                // Commit after each update
                                using (OracleCommand commitCmd = _oraConn.CreateCommand())
                                {
                                    commitCmd.CommandText = "COMMIT";
                                    commitCmd.ExecuteNonQuery();
                                }

                                iUpdateCount++;
                            }
                        }
                    }
                }

                string countMessage = $"    update pxxt_code s_input_hosp_id:[{_sInputHospId}] i_update_count:[{iUpdateCount}] ";
                Console.WriteLine(countMessage);
                _logger.Info(countMessage);

                // Update pxxt_code if needed
                if (iUpdateCount > 0 && 
                    (string.IsNullOrEmpty(_sInputHospId) || 
                     _sInputHospId.ToUpper() == "ALL"))
                {
                    StringBuilder updatePxxtSQL = new StringBuilder();
                    using (OracleCommand updatePxxtCmd = _oraConn.CreateCommand())
                    {
                        updatePxxtSQL.AppendLine("UPDATE pxxt_code");
                        updatePxxtSQL.AppendLine("   SET EXE_TIME = SYSDATE");
                        updatePxxtSQL.AppendLine(" WHERE sub_sys = 'ICE'");
                        updatePxxtSQL.AppendLine("   AND data_type = '110'");
                        updatePxxtSQL.AppendLine("   AND code = '1'");

                        updatePxxtCmd.CommandText = updatePxxtSQL.ToString();
                        int rowsAffected = updatePxxtCmd.ExecuteNonQuery();

                        string message = $"update pxxt_code ICE data_type='110' code='1' sqlcode:[0] rec:[{rowsAffected}] ";
                        Console.WriteLine(message);
                        _logger.Info(message);

                        // Commit the update
                        using (OracleCommand commitCmd = _oraConn.CreateCommand())
                        {
                            commitCmd.CommandText = "COMMIT";
                            commitCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            finally
            {
                if (_oraConn.State == ConnectionState.Open)
                {
                    _oraConn.Close();
                }
            }
        }

        private static void UpdateMark560()
        {
            try
            {
                _oraConn.Open();

                // Get hospital IDs
                StringBuilder strSQL = new StringBuilder();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("SELECT hosp_id");
                    strSQL.AppendLine("  FROM mhat_hospbsc a");
                    strSQL.AppendLine(" WHERE EXISTS ( SELECT 1");
                    strSQL.AppendLine("                  FROM RAPI_ASSAY_DATA_FINAL b");
                    strSQL.AppendLine("                 WHERE a.hosp_id = b.hosp_id");
                    strSQL.AppendLine("                   AND b.fee_ym = TO_DATE(:sInputFeeYm,'YYYYMMDD')");
                    cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                    strSQL.AppendLine("                   AND ( hosp_id = :sInputHospId OR");
                    cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                    strSQL.AppendLine("                         'ALL' = UPPER(:sInputHospIdUpper) OR");
                    cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId));
                    strSQL.AppendLine("                         NVL(:sInputHospIdNull,0) = 0 )");
                    cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? "0" : _sInputHospId));
                    strSQL.AppendLine("               )");
                    strSQL.AppendLine(" ORDER BY hosp_id ASC");

                    cmd.CommandText = strSQL.ToString();

                    Console.WriteLine($"    1 - UpdateMark560() - select mhat_hospbsc s_exec_flag:[{_sExecFlag}] s_input_fee_ym:[{_sInputFeeYm}] s_input_hosp_id(選項):[{_sInputHospId}]");

                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        int iHospIdCount = 0;
                        while (reader.Read())
                        {
                            string hospId = reader["hosp_id"].ToString();
                            string message = $"    update hosp_id[{iHospIdCount}]:[{hospId}] ";
                            Console.Write(message);
                            _logger.Info(message);

                            // Update RAPI_ASSAY_DATA_FINAL for each hospital
                            StringBuilder updateSQL = new StringBuilder();
                            using (OracleCommand updateCmd = _oraConn.CreateCommand())
                            {
                                updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                                updateSQL.AppendLine("   SET mark_560='Y'");
                                updateSQL.AppendLine(" WHERE hosp_id = :hospId");
                                updateCmd.Parameters.Add(new OracleParameter("hospId", hospId));
                                updateSQL.AppendLine("   AND fee_ym = TO_DATE(:sInputFeeYm,'YYYYMMDD')");
                                updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                                updateSQL.AppendLine("   AND mark_560 IS NULL");
                                updateSQL.AppendLine("   AND order_code IN (");
                                updateSQL.AppendLine("       SELECT code FROM pxxt_code");
                                updateSQL.AppendLine("        WHERE sub_sys = 'PBA' AND data_type = '560'");
                                updateSQL.AppendLine("        UNION");
                                updateSQL.AppendLine("       SELECT '64164B' code FROM dual UNION");
                                updateSQL.AppendLine("       SELECT '64169B' code FROM dual UNION");
                                updateSQL.AppendLine("       SELECT '64202B' code FROM dual UNION");
                                updateSQL.AppendLine("       SELECT '64170B' code FROM dual UNION");
                                updateSQL.AppendLine("       SELECT '64162B' code FROM dual UNION");
                                updateSQL.AppendLine("       SELECT '64258B' code FROM dual UNION");
                                updateSQL.AppendLine("       SELECT '64201B' code FROM dual )");

                                updateCmd.CommandText = updateSQL.ToString();
                                int rowsAffected = updateCmd.ExecuteNonQuery();

                                string resultMessage = $" sqlcode:[0] rec:[{rowsAffected}] ";
                                Console.WriteLine(resultMessage);
                                _logger.Info(resultMessage);

                                // Commit after each update
                                using (OracleCommand commitCmd = _oraConn.CreateCommand())
                                {
                                    commitCmd.CommandText = "COMMIT";
                                    commitCmd.ExecuteNonQuery();
                                }
                            }

                            iHospIdCount++;
                        }

                        Console.WriteLine($"        i_hosp_id_count:[{iHospIdCount}] sqlcode:[0] rec:[{iHospIdCount}] ");
                    }
                }
            }
            finally
            {
                if (_oraConn.State == ConnectionState.Open)
                {
                    _oraConn.Close();
                }
            }
        }

        private static void UpdateMark24hrPBA560()
        {
            int iLoopCount = 0;
            _iExeType = 1;

            Console.WriteLine($"    0 - UpdateMark24hrPBA560() - select pxxt_code s_exec_flag:[{_sExecFlag}] s_input_hosp_id:[{_sInputHospId}] s_input_fee_ym:[{_sInputFeeYm}] ");

            try
            {
                _oraConn.Open();

                StringBuilder strSQL = new StringBuilder();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("SELECT code order_code");
                    strSQL.AppendLine("  FROM pxxt_code");
                    strSQL.AppendLine(" WHERE sub_sys = 'PBA'");
                    strSQL.AppendLine("   AND data_type = '560'");
                    strSQL.AppendLine("   AND valid_s_date = TO_DATE(:validSDate,'yyyymmdd')");
                    cmd.Parameters.Add(new OracleParameter("validSDate", _sValidSDate));

                    cmd.CommandText = strSQL.ToString();

                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string orderCode = reader["order_code"].ToString();

                            string message = $"    update s_mark_24hr:[{_sMark24hr}] s_input_hosp_id:[{_sInputHospId}] s_input_fee_ym:[{_sInputFeeYm}] wk_order_code:[{orderCode}] wk_branch_code:[{_wkBranchCode}] ";
                            Console.WriteLine(message);
                            _logger.Info(message);

                            StringBuilder updateSQL = new StringBuilder();
                            using (OracleCommand updateCmd = _oraConn.CreateCommand())
                            {
                                updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL a");
                                updateSQL.AppendLine("   SET MARK_24HR = :sMark24hr");
                                updateCmd.Parameters.Add(new OracleParameter("sMark24hr", _sMark24hr));
                                updateSQL.AppendLine(" WHERE ( a.hosp_id = :sInputHospId OR UPPER(:sInputHospIdUpper) = 'ALL' OR NVL(:sInputHospIdNull,0)=0 )");
                                updateCmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                                updateCmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId));
                                updateCmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? "0" : _sInputHospId));
                                updateSQL.AppendLine("   AND a.fee_ym = TO_DATE(:sInputFeeYm,'YYYYMMDD')");
                                updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                                updateSQL.AppendLine("   AND a.order_code = :orderCode");
                                updateCmd.Parameters.Add(new OracleParameter("orderCode", orderCode));
                                updateSQL.AppendLine("   AND a.BRANCH_CODE = ( CASE WHEN :wkBranchCode BETWEEN '1' AND '6' THEN :wkBranchCodeValue ELSE a.BRANCH_CODE END )");
                                updateCmd.Parameters.Add(new OracleParameter("wkBranchCode", _wkBranchCode));
                                updateCmd.Parameters.Add(new OracleParameter("wkBranchCodeValue", _wkBranchCode));
                                updateSQL.AppendLine("   AND a.MARK_24HR = '5'");
                                updateSQL.AppendLine("   AND TO_DATE(SUBSTR(a.INIT_WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD') < LAST_DAY(ADD_MONTHS(A.fee_ym,1))+1");

                                updateCmd.CommandText = updateSQL.ToString();
                                int rowsAffected = updateCmd.ExecuteNonQuery();

                                string resultMessage = $"    sqlcode:[0] rec:[{rowsAffected}] ";
                                Console.WriteLine(resultMessage);
                                _logger.Info(resultMessage);

                                // Commit after each update
                                using (OracleCommand commitCmd = _oraConn.CreateCommand())
                                {
                                    commitCmd.CommandText = "COMMIT";
                                    commitCmd.ExecuteNonQuery();
                                }

                                iLoopCount++;
                            }
                        }
                    }
                }

                string countMessage = $"    update RAPI_ASSAY_DATA_FINAL i_loop_count:[{iLoopCount}] ";
                Console.WriteLine(countMessage);
                _logger.Info(countMessage);
            }
            finally
            {
                if (_oraConn.State == ConnectionState.Open)
                {
                    _oraConn.Close();
                }
            }
        }

        private static void CheckArg(string[] args)
        {
            if (args.Length < 1)
            {
                _proList.exitCode = 1;
                _proList.message = "參數個數不符";
                ShowUsage();
                throw new ArgumentException(_proList.message);
            }

            _sExecFlag = args[0];
            Console.WriteLine($" s_exec_flag:[{_sExecFlag}] argc:[{args.Length}] ");

            if (args.Length >= 2)
            {
                _sInputFeeYm = args[1];
                Console.WriteLine($"    s_input_fee_ym:[{_sInputFeeYm}] ");
            }

            if (args.Length >= 3)
            {
                _sInputHospId = args[2];
                Console.WriteLine($"    s_input_hosp_id:[{_sInputHospId}] ");
            }

            if (args.Length >= 4)
            {
                _sValidSDate = args[3];
                Console.WriteLine($"    s_valid_s_date:[{_sValidSDate}] ");
            }

            if (args.Length >= 5)
            {
                _sMark24hr = args[4];
                Console.WriteLine($"    s_mark_24hr:[{_sMark24hr}] ");
            }

            if (args.Length >= 6)
            {
                _wkBranchCode = args[5];
                Console.WriteLine($"    wk_branch_code:[{_wkBranchCode}] ");
            }

            // Validate arguments
            if ((_sExecFlag == "0" || _sExecFlag == "1") && (args.Length <= 1 || args.Length >= 4))
            {
                _proList.exitCode = 1;
                ShowUsage();
                throw new ArgumentException("參數個數不符");
            }

            if (_sExecFlag == "2" && (args.Length != 5 && args.Length != 6))
            {
                _proList.exitCode = 2;
                ShowUsage();
                throw new ArgumentException("參數個數不符");
            }

            if (!(_sExecFlag == "0" || _sExecFlag == "1" || _sExecFlag == "2"))
            {
                _proList.exitCode = 3;
                ShowUsage();
                throw new ArgumentException("執行類別參數錯誤");
            }

            if (args.Length == 6 &&
                !(_wkBranchCode == "1" || _wkBranchCode == "2" || _wkBranchCode == "3" ||
                  _wkBranchCode == "4" || _wkBranchCode == "5" || _wkBranchCode == "6"))
            {
                _proList.exitCode = 4;
                ShowUsage();
                throw new ArgumentException("分區別參數錯誤");
            }
        }

        private static void ShowUsage()
        {
            var prog = AppDomain.CurrentDomain.FriendlyName;
            Console.WriteLine("執行類別： 0 - update MARK_24HR = 8 ( 24小時上傳註記: 8-例外院所 ) ");
            Console.WriteLine("           1 - update mark_560      ( Y : 醫令代碼 為 重要檢驗（查）結果之項目 ) ");
            Console.WriteLine(" ");
            Console.WriteLine("參數種類： 程式代號  執行類別  費用年月  醫事機構代碼(選項)    ");
            Console.WriteLine(" ");
            Console.WriteLine("範例1   ： icei2029b01  0  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 ");
            Console.WriteLine("範例2   ： icei2029b01  0  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 ");
            Console.WriteLine("範例3   ： icei2029b01  0  20190101  [all]        *參數給all，全部院所重新執行。  ");
            Console.WriteLine(" ");
            Console.WriteLine("參數種類： 程式代號  執行類別  費用年月  醫事機構代碼(選項)    ");
            Console.WriteLine(" ");
            Console.WriteLine("範例4   ： icei2029b01  1  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 ");
            Console.WriteLine("範例5   ： icei2029b01  1  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 ");
            Console.WriteLine("範例6   ： icei2029b01  1  20190101  [all]        *參數給all，全部院所重新執行。  ");
            Console.WriteLine("參數種類： 程式代號  執行類別  費用年月  醫事機構代碼(選項)  有效迄日 更新註記  分區別(選項)  ");
            Console.WriteLine(" ");
            Console.WriteLine("範例7   ： icei2029b01  2  20190101  \"\"       20190101  A     *未給醫事機構代碼，只做尚未計算給付上限的院所。 ");
            Console.WriteLine("範例8   ： icei2029b01  2  20190101  [hosp_id]  20190101  A     *提供醫事機構代碼，只做該院所。 ");
            Console.WriteLine("範例9   ： icei2029b01  2  20190101  [all]      20190101  A  2  *參數給all，全部院所重新執行。  ");
        }
    }
}
```