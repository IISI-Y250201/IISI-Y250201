```csharp
/*******************************************************************
程式代碼：icei2047b03
程式名稱：住診即時上傳間隔天數計算邏輯放寬作業
功能簡述：住診即時上傳間隔天數計算邏輯放寬作業
參    數：
參數一：程式代號 分區別 費用年月(西元)
範例一：icei2047b03 1 202110
讀取檔案：無
異動檔案：RAPI_ASSAY_DATA_FINAL
作    者：
歷次修改時間：
1.
需求單號暨修改內容簡述：
1.
備    註：
********************************************************************/

using System;
using System.Data;
using System.Text;
using NLog;
using Oracle.ManagedDataAccess.Client;
using NHI.MEDCS.MEDM.Common;

namespace icei2047b03
{
    public class icei2047b03
    {
        /* ---------- static members ---------- */
        private static OracleConnection _oraConn =
            new OracleConnection(GetDBInfo.GetHmDBConnectString);

        private static Logger _logger =
            LogManager.GetLogger(AppDomain.CurrentDomain.FriendlyName.Substring(0, 3).ToUpper());

        private class ProList
        {
            public int exitCode = -999;
            public string message = string.Empty;
        }
        private static ProList _proList = new ProList();

        /* ---------- variables ---------- */
        private static string _hospId = string.Empty;
        private static string _branchCode = string.Empty;
        private static string _feeYm = string.Empty;
        private static string _feeYm3061 = string.Empty;
        private static string _startTime = string.Empty;
        private static string _endTime = string.Empty;

        /* ---------- Main ---------- */
        static void Main(string[] args)
        {
            try
            {
                // Original: PXX_start_process();
                MEDM_SysLib.MEDM_StartProcess();

                GetSystemDate();
                CheckArg(args);
                ProcessHospitalData();

                _proList.exitCode = 0;
                _proList.message = $"\n程式 {AppDomain.CurrentDomain.FriendlyName} 結束\n";
                Console.WriteLine(_proList.message);
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
                // Original: PXX_exit_process(rtn_code, 0, msg);
                MEDM_SysLib.MEDM_ExitProcess(_proList.exitCode, _proList.message);
            }
        }

        /* ---------- parameter check ---------- */
        // Original: check_arg(int argc, char *argv[])
        private static void CheckArg(string[] args)
        {
            if (args.Length != 2)
            {
                _proList.exitCode = 1;
                _proList.message = "參數個數不符";
                WriteUsage();
                throw new ArgumentException(_proList.message);
            }

            if (string.Compare(args[0], "1") < 0 || string.Compare(args[0], "6") > 0)
            {
                _proList.exitCode = 3;
                _proList.message = $"分區有誤<{args[0]}>";
                WriteMsg(_proList.message);
                WriteUsage();
                throw new ArgumentException(_proList.message);
            }
            _branchCode = args[0];

            if (string.Compare(args[1], "202110") < 0 || args[1].Length != 6)
            {
                _proList.exitCode = 3;
                _proList.message = $"費用年月(西元)有誤<{args[1]}>";
                WriteMsg(_proList.message);
                WriteUsage();
                throw new ArgumentException(_proList.message);
            }
            _feeYm = args[1];

            WriteMsg($"參數1：分區<{_branchCode}>\n" +
                     $"參數2：費用年月<{_feeYm}>");

            _logger.Info($"Args → {string.Join(",", args)}");
        }

        // Original: write_msg(char *fmt, ...)
        private static void WriteMsg(string message)
        {
            Console.WriteLine(message);
            _logger.Info(message);
        }

        private static void WriteUsage()
        {
            var prog = AppDomain.CurrentDomain.FriendlyName;
            Console.WriteLine("參數種類：   程式代號   分區  費用年月(西元) ");
            Console.WriteLine($"範例    ： {prog}    1     202110       ");
        }

        // Original: SQL select to_char(sysdate,'yyyy/mm/dd hh24:mi:ss') into :start_time from dual
        private static void GetSystemDate()
        {
            _startTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
        }

        // Original: Main processing logic
        private static void ProcessHospitalData()
        {
            StringBuilder strSQL = new StringBuilder();
            
            _oraConn.Open();

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                // Get hospital IDs
                strSQL.Clear();
                strSQL.AppendLine("SELECT HOSP_ID");
                strSQL.AppendLine("  FROM ICEI_3060_PBA_CTL");
                strSQL.AppendLine(" WHERE FEE_YM = TO_DATE(:feeYm,'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND BRANCH_CODE = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _branchCode));
                strSQL.AppendLine("   AND OUT_DATE_MARK = 'Y'");
                strSQL.AppendLine("   AND SUBSTR(HOSP_DATA_TYPE,1,1) = '2'");
                strSQL.AppendLine(" GROUP BY HOSP_ID");

                cmd.CommandText = strSQL.ToString();

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _hospId = reader.GetString(0);
                        WriteMsg($"hosp_id<{_hospId}> fee_ym<{_feeYm}>");

                        // Process each hospital
                        UpdateHospitalData();
                        
                        // Call batch process
                        _feeYm3061 = (int.Parse(_feeYm) - 191100).ToString();
                        WriteMsg($"呼叫IPM執行程式：icei3061b01 {_branchCode} {_feeYm3061} {_hospId}");
                        
                        // Original: PXX_exec_batch("icei3061b01", h_branch_code, h_fee_ym_3061, h_hosp_id, NULL);
                        MEDM_SysLib.MEDM_ExecBatch("icei3061b01", _branchCode, _feeYm3061, _hospId);
                    }
                }
            }

            // Commit transaction
            using (OracleCommand cmdCommit = _oraConn.CreateCommand())
            {
                cmdCommit.CommandText = "COMMIT";
                cmdCommit.ExecuteNonQuery();
            }

            // Get end time
            _endTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            WriteMsg($" {_proList.message}\n ***start_time<{_startTime}>\n ***  end_time<{_endTime}>");

            _oraConn.Close();
        }

        private static void UpdateHospitalData()
        {
            StringBuilder strSQL = new StringBuilder();
            
            // Update 1: MARK_24HR = 'D'
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.Clear();
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'D'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND BRANCH_CODE = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _branchCode));
                strSQL.AppendLine("   AND FEE_YM  = TO_DATE(:feeYm,'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND (RECARD_MARK = '2' AND TREAT_DT > CASE_TIME)");
                strSQL.AppendLine("   AND TREAT_DT > NVL(OUT_DATE,APPL_E_DATE)");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-TREAT_DT <= 2");

                cmd.CommandText = strSQL.ToString();
                cmd.ExecuteNonQuery();
            }

            // Update 2: MARK_24HR = 'E'
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.Clear();
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'E'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND BRANCH_CODE = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _branchCode));
                strSQL.AppendLine("   AND FEE_YM  = TO_DATE(:feeYm,'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND (RECARD_MARK = '2' AND TREAT_DT > CASE_TIME)");
                strSQL.AppendLine("   AND TREAT_DT BETWEEN IN_DATE AND NVL(OUT_DATE,APPL_E_DATE)");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-NVL(OUT_DATE,APPL_E_DATE) <= 2");

                cmd.CommandText = strSQL.ToString();
                cmd.ExecuteNonQuery();
            }

            // Update 3: MARK_24HR = 'A'
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.Clear();
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'A'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND BRANCH_CODE = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _branchCode));
                strSQL.AppendLine("   AND FEE_YM  = TO_DATE(:feeYm,'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND REAL_RECV_DATE IS NOT NULL");
                strSQL.AppendLine("   AND REAL_RECV_DATE > NVL(OUT_DATE,APPL_E_DATE)");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-REAL_RECV_DATE <= 2");

                cmd.CommandText = strSQL.ToString();
                cmd.ExecuteNonQuery();
            }

            // Update 4: MARK_24HR = 'A' (second case)
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.Clear();
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'A'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND BRANCH_CODE = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _branchCode));
                strSQL.AppendLine("   AND FEE_YM  = TO_DATE(:feeYm,'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND REAL_RECV_DATE IS NULL");
                strSQL.AppendLine("   AND CASE_TIME > NVL(OUT_DATE,APPL_E_DATE)");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-CASE_TIME <= 2");

                cmd.CommandText = strSQL.ToString();
                cmd.ExecuteNonQuery();
            }

            // Update 5: MARK_24HR = 'B'
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.Clear();
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'B'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND BRANCH_CODE = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _branchCode));
                strSQL.AppendLine("   AND FEE_YM  = TO_DATE(:feeYm,'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND REAL_RECV_DATE IS NOT NULL");
                strSQL.AppendLine("   AND REAL_RECV_DATE < IN_DATE");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-NVL(OUT_DATE,APPL_E_DATE) <= 2");

                cmd.CommandText = strSQL.ToString();
                cmd.ExecuteNonQuery();
            }

            // Update 6: MARK_24HR = 'B' (second case)
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.Clear();
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'B'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND BRANCH_CODE = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _branchCode));
                strSQL.AppendLine("   AND FEE_YM  = TO_DATE(:feeYm,'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND REAL_RECV_DATE IS NULL");
                strSQL.AppendLine("   AND CASE_TIME < IN_DATE");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-NVL(OUT_DATE,APPL_E_DATE) <= 2");

                cmd.CommandText = strSQL.ToString();
                cmd.ExecuteNonQuery();
            }

            // Update 7: MARK_24HR = 'C'
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.Clear();
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'C'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND BRANCH_CODE = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _branchCode));
                strSQL.AppendLine("   AND FEE_YM  = TO_DATE(:feeYm,'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND REAL_RECV_DATE IS NOT NULL");
                strSQL.AppendLine("   AND REAL_RECV_DATE BETWEEN IN_DATE AND NVL(OUT_DATE,APPL_E_DATE)");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-NVL(OUT_DATE,APPL_E_DATE) <= 2");

                cmd.CommandText = strSQL.ToString();
                cmd.ExecuteNonQuery();
            }

            // Update 8: MARK_24HR = 'C' (second case)
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.Clear();
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'C'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND BRANCH_CODE = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _branchCode));
                strSQL.AppendLine("   AND FEE_YM  = TO_DATE(:feeYm,'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND REAL_RECV_DATE IS NULL");
                strSQL.AppendLine("   AND CASE_TIME BETWEEN IN_DATE AND NVL(OUT_DATE,APPL_E_DATE)");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-NVL(OUT_DATE,APPL_E_DATE) <= 2");

                cmd.CommandText = strSQL.ToString();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
```