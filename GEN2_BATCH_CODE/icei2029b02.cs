```csharp
/*******************************************************************
程式代碼：icei2029b02
程式名稱：24小時上傳註記勾稽作業
功能簡述：更新24小時上傳註記及重要檢驗結果項目標記
參    數：
參數一：程式代號 執行類別 費用年月 醫事機構代碼(選項) 有效迄日 更新註記 分區別(選項)
範例一：icei2029b02 0 20190101 ALL
讀取檔案：無
異動檔案：無
作    者：系統轉換
歷次修改時間：
1.20240101
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

namespace icei2029b02
{
    public class icei2029b02
    {
        /* ---------- static members ---------- */
        private static OracleConnection _oraConn = new OracleConnection(GetDBInfo.GetHmDBConnectString);
        private static Logger _logger = LogManager.GetLogger(AppDomain.CurrentDomain.FriendlyName.Substring(0, 3).ToUpper());

        private class ProList
        {
            public int exitCode = -999;
            public string message = string.Empty;
        }
        private static ProList _proList = new ProList();

        /* ---------- variables ---------- */
        private static string _inputFeeYm = string.Empty;
        private static string _inputHospId = string.Empty;
        private static string _validSDate = string.Empty;
        private static string _mark24hr = string.Empty;
        private static string _branchCode = string.Empty;
        private static string _execFlag = string.Empty;
        private static int _exeType = 1;

        /* ---------- Main ---------- */
        static void Main(string[] args)
        {
            try
            {
                // PXX_start_process();
                MEDM_SysLib.MEDM_StartProcess();

                Console.WriteLine("========== icei2029b02 start ========== ");

                CheckArg(args);

                if (_execFlag == "0")
                {
                    UpdateMark24Hr();
                }

                if (_execFlag == "1")
                {
                    UpdateMark560();
                }

                if (_execFlag == "2")
                {
                    UpdateMark24hrPBA560();
                }

                // 1100629 ADD 報告日期CASE_TIME 在出院日期後
                if (_execFlag == "3")
                {
                    UpdateMark24hrHPA();
                }

                // 1100629 ADD REAL_RECV_DATE(實際收到報告日期時間)有值者 在出院日期後
                if (_execFlag == "7")
                {
                    UpdateMark24hrHPAA();
                }

                // 1100629 ADD 報告日期CASE_TIME 在入院日期前
                if (_execFlag == "4")
                {
                    UpdateMark24hrHPB();
                }

                // 1100629 ADD REAL_RECV_DATE(實際收到報告日期時間)有值者 在入院日期前
                if (_execFlag == "8")
                {
                    UpdateMark24hrHPBB();
                }

                // 1100629 ADD 報告日期CASE_TIME 在住院期間
                if (_execFlag == "5")
                {
                    UpdateMark24hrHPC();
                }

                // 1100629 ADD EAL_RECV_DATE(實際收到報告日期時間)有值者 在住院期間
                if (_execFlag == "9")
                {
                    UpdateMark24hrHPCC();
                }

                // 1100701 ADD Clear mark_24hr in ('A','B','C')
                if (_execFlag == "6")
                {
                    UpdateMark24hrHPClear();
                }

                // 1100629 ADD RECARD_MARK(補卡註記)=2且TREAT_DT> CASE_TIME者 TREAT_DT_TIME>出院日期
                if (_execFlag == "A")
                {
                    UpdateMark24hrHPD();
                }

                // 1100629 ADD RECARD_MARK(補卡註記)=2且TREAT_DT> CASE_TIME者 在住院期間
                if (_execFlag == "B")
                {
                    UpdateMark24hrHPE();
                }

                // 1100629 ADD icei_assay_dl6_mst 重新啟動
                if (_execFlag == "C")
                {
                    UpdateIceiAssayDl6MstReset();
                }

                Console.WriteLine("========== icei2029b02 end ========== ");

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

        /* ---------- parameter check ---------- */
        private static void CheckArg(string[] args)
        {
            if (args.Length < 1)
            {
                _proList.exitCode = 1;
                _proList.message = "參數個數不符";
                ShowUsage();
                throw new ArgumentException();
            }

            _execFlag = args[0];
            Console.WriteLine($" _execFlag:[{_execFlag}] argc:[{args.Length}] ");

            if (args.Length >= 3)
            {
                _inputFeeYm = args[1];
                Console.WriteLine($"    _inputFeeYm:[{_inputFeeYm}] ");
            }

            if (args.Length >= 4)
            {
                _inputHospId = args[2];
                Console.WriteLine($"    _inputHospId:[{_inputHospId}] ");
            }

            if (args.Length >= 5)
            {
                _validSDate = args[3];
                Console.WriteLine($"    _validSDate:[{_validSDate}] ");
            }

            if (args.Length >= 6)
            {
                _mark24hr = args[4];
                Console.WriteLine($"    _mark24hr:[{_mark24hr}] ");
            }

            if (args.Length >= 7)
            {
                _branchCode = args[5];
                Console.WriteLine($"    _branchCode:[{_branchCode}] ");
            }

            if ((_execFlag == "0" || _execFlag == "1") && (args.Length <= 2 || args.Length >= 5))
            {
                _proList.exitCode = 1;
                ShowUsage();
                throw new ArgumentException();
            }

            if (_execFlag == "2" && (args.Length != 6 && args.Length != 7))
            {
                _proList.exitCode = 2;
                ShowUsage();
                throw new ArgumentException();
            }

            // 1100629 ADD
            if (!(_execFlag == "0" || _execFlag == "1" || _execFlag == "2" ||
                  _execFlag == "3" || _execFlag == "4" || _execFlag == "5" ||
                  _execFlag == "6" || _execFlag == "7" || _execFlag == "8" ||
                  _execFlag == "9" || _execFlag == "A" || _execFlag == "B" ||
                  _execFlag == "C"))
            {
                _proList.exitCode = 3;
                ShowUsage();
                throw new ArgumentException();
            }

            if (args.Length == 7 &&
                !(_branchCode == "1" || _branchCode == "2" || _branchCode == "3" ||
                  _branchCode == "4" || _branchCode == "5" || _branchCode == "6"))
            {
                _proList.exitCode = 4;
                ShowUsage();
                throw new ArgumentException();
            }

            _logger.Info($"Args → {string.Join(",", args)}");
        }

        private static void ShowUsage()
        {
            var prog = AppDomain.CurrentDomain.FriendlyName;
            Console.WriteLine("執行類別： 0 - update MARK_24HR = 8 ( 24小時上傳註記: 8-例外院所 ) ");
            Console.WriteLine("           1 - update mark_560      ( Y : 醫令代碼 為 重要檢驗（查）結果之項目 ) ");
            Console.WriteLine(" ");
            Console.WriteLine("參數種類： 程式代號  執行類別  費用年月  醫事機構代碼(選項)    ");
            Console.WriteLine(" ");
            Console.WriteLine("範例1   ： icei2029b02  0  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 ");
            Console.WriteLine("範例2   ： icei2029b02  0  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 ");
            Console.WriteLine("範例3   ： icei2029b02  0  20190101  [all]        *參數給all，全部院所重新執行。  ");
            Console.WriteLine(" ");
            Console.WriteLine("參數種類： 程式代號  執行類別  費用年月  醫事機構代碼(選項)    ");
            Console.WriteLine(" ");
            Console.WriteLine("範例4   ： icei2029b02  1  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 ");
            Console.WriteLine("範例5   ： icei2029b02  1  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 ");
            Console.WriteLine("範例6   ： icei2029b02  1  20190101  [all]        *參數給all，全部院所重新執行。  ");
            Console.WriteLine("參數種類： 程式代號  執行類別  費用年月  醫事機構代碼(選項)  有效迄日 更新註記  分區別(選項)  ");
            Console.WriteLine(" ");
            Console.WriteLine("範例7   ： icei2029b02  2  20190101  \"\"       20190101  A     *未給醫事機構代碼，只做尚未計算給付上限的院所。 ");
            Console.WriteLine("範例8   ： icei2029b02  2  20190101  [hosp_id]  20190101  A     *提供醫事機構代碼，只做該院所。 ");
            Console.WriteLine("範例9   ： icei2029b02  2  20190101  [all]      20190101  A  2  *參數給all，全部院所重新執行。  ");
            Console.WriteLine(" ");
            Console.WriteLine("範例1   ： icei2029b02  3  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 ");
            Console.WriteLine("範例2   ： icei2029b02  3  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 ");
            Console.WriteLine("範例3   ： icei2029b02  3  20190101  [all]        *參數給all，全部院所重新執行。  ");
            Console.WriteLine(" ");
            Console.WriteLine("範例1   ： icei2029b02  4  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 ");
            Console.WriteLine("範例2   ： icei2029b02  4  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 ");
            Console.WriteLine("範例3   ： icei2029b02  4  20190101  [all]        *參數給all，全部院所重新執行。  ");
            Console.WriteLine(" ");
            Console.WriteLine("範例1   ： icei2029b02  5  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 ");
            Console.WriteLine("範例2   ： icei2029b02  5  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 ");
            Console.WriteLine("範例3   ： icei2029b02  5  20190101  [all]        *參數給all，全部院所重新執行。  ");
            Console.WriteLine(" ");
            Console.WriteLine("範例1   ： icei2029b02  6  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 ");
            Console.WriteLine("範例2   ： icei2029b02  6  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 ");
            Console.WriteLine("範例3   ： icei2029b02  6  20190101  [all]        *參數給all，全部院所重新執行。  ");
        }

        #region SQL Structures
        private class SQL100
        {
            public string hospId { get; set; } = string.Empty;
            public string recvSDate { get; set; } = string.Empty;
            public string recvEDate { get; set; } = string.Empty;
            public string feeSDate { get; set; } = string.Empty;
            public string feeEDate { get; set; } = string.Empty;
        }
        #endregion

        #region Business Logic Methods
        private static void UpdateMark24Hr()
        {
            int updateCount = 0;
            _exeType = 1;

            Console.WriteLine($"    0 - UpdateMark24Hr() - select pxxt_code _execFlag:[{_execFlag}] _inputHospId:[{_inputHospId}] _inputFeeYm:[{_inputFeeYm}] ");

            _oraConn.Open();
            try
            {
                StringBuilder strSQL = new StringBuilder();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("SELECT CODE HOSP_ID,");
                    strSQL.AppendLine("       TO_CHAR(VALID_S_DATE,'YYYYMMDD') RECV_S_DATE,");
                    strSQL.AppendLine("       TO_CHAR(VALID_E_DATE,'YYYYMMDD') RECV_E_DATE,");
                    strSQL.AppendLine("       TO_CHAR(VALID_S_DATE,'YYYYMM')||'01' FEE_S_DATE,");
                    strSQL.AppendLine("       TO_CHAR(VALID_E_DATE,'YYYYMM')||'01' FEE_E_DATE");
                    strSQL.AppendLine("  FROM PXXT_CODE");
                    strSQL.AppendLine(" WHERE SUB_SYS = 'ICE'");
                    strSQL.AppendLine("   AND DATA_TYPE = '109'");
                    strSQL.AppendLine("   AND TRUNC(TO_DATE(:inputFeeYm,'YYYYMMDD'),'MM') BETWEEN TRUNC(VALID_S_DATE,'MM') AND TRUNC(VALID_E_DATE,'MM')");
                    cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                    strSQL.AppendLine("   AND (CODE = :inputHospId OR");
                    cmd.Parameters.Add(new OracleParameter("inputHospId", _inputHospId));
                    strSQL.AppendLine("        'ALL' = UPPER(:inputHospId2) OR");
                    cmd.Parameters.Add(new OracleParameter("inputHospId2", _inputHospId));
                    strSQL.AppendLine("        (NVL(:inputHospId3,0) = 0 AND");
                    cmd.Parameters.Add(new OracleParameter("inputHospId3", _inputHospId));
                    strSQL.AppendLine("         EXE_TIME >= (SELECT EXE_TIME");
                    strSQL.AppendLine("                        FROM PXXT_CODE");
                    strSQL.AppendLine("                       WHERE SUB_SYS = 'ICE'");
                    strSQL.AppendLine("                         AND DATA_TYPE = '110'");
                    strSQL.AppendLine("                         AND CODE = '1')))");

                    cmd.CommandText = strSQL.ToString();
                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SQL100 sql100 = new SQL100
                            {
                                hospId = reader["HOSP_ID"].ToString(),
                                recvSDate = reader["RECV_S_DATE"].ToString(),
                                recvEDate = reader["RECV_E_DATE"].ToString(),
                                feeSDate = reader["FEE_S_DATE"].ToString(),
                                feeEDate = reader["FEE_E_DATE"].ToString()
                            };

                            StringBuilder updateSQL = new StringBuilder();
                            using (OracleCommand updateCmd = _oraConn.CreateCommand())
                            {
                                updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL A");
                                updateSQL.AppendLine("   SET MARK_24HR = '8'");
                                updateSQL.AppendLine(" WHERE (A.HOSP_ID = :hospId OR :hospId2 = '0000000000')");
                                updateCmd.Parameters.Add(new OracleParameter("hospId", sql100.hospId));
                                updateCmd.Parameters.Add(new OracleParameter("hospId2", sql100.hospId));
                                updateSQL.AppendLine("   AND A.FEE_YM BETWEEN TO_DATE(:feeSDate,'YYYYMMDD') AND TO_DATE(:feeEDate,'YYYYMMDD')");
                                updateCmd.Parameters.Add(new OracleParameter("feeSDate", sql100.feeSDate));
                                updateCmd.Parameters.Add(new OracleParameter("feeEDate", sql100.feeEDate));
                                updateSQL.AppendLine("   AND A.CASE_TIME BETWEEN TO_DATE(:recvSDate,'YYYYMMDD') AND TO_DATE(:recvEDate,'YYYYMMDD')");
                                updateCmd.Parameters.Add(new OracleParameter("recvSDate", sql100.recvSDate));
                                updateCmd.Parameters.Add(new OracleParameter("recvEDate", sql100.recvEDate));
                                updateSQL.AppendLine("   AND A.MARK_24HR = '5'");
                                updateSQL.AppendLine("   AND A.RECV_D_NUM > 0");

                                updateCmd.CommandText = updateSQL.ToString();
                                int rowsAffected = updateCmd.ExecuteNonQuery();

                                string message = $"    update hosp_id:[{sql100.hospId}] fee_s_date:[{sql100.feeSDate}] fee_e_date:[{sql100.feeEDate}] recv_s_date:[{sql100.recvSDate}] recv_e_date:[{sql100.recvEDate}] sqlcode:[0] rec:[{rowsAffected}] ";
                                Console.WriteLine(message);
                                _logger.Info(message);

                                updateCount++;
                            }
                        }
                    }
                }

                string updateMessage = $"    update pxxt_code _inputHospId:[{_inputHospId}] updateCount:[{updateCount}] ";
                Console.WriteLine(updateMessage);
                _logger.Info(updateMessage);

                if (updateCount > 0 &&
                    (string.IsNullOrEmpty(_inputHospId) ||
                     _inputHospId.ToUpper() == "ALL"))
                {
                    StringBuilder updatePxxtSQL = new StringBuilder();
                    using (OracleCommand updatePxxtCmd = _oraConn.CreateCommand())
                    {
                        updatePxxtSQL.AppendLine("UPDATE PXXT_CODE");
                        updatePxxtSQL.AppendLine("   SET EXE_TIME = SYSDATE");
                        updatePxxtSQL.AppendLine(" WHERE SUB_SYS = 'ICE'");
                        updatePxxtSQL.AppendLine("   AND DATA_TYPE = '110'");
                        updatePxxtSQL.AppendLine("   AND CODE = '1'");

                        updatePxxtCmd.CommandText = updatePxxtSQL.ToString();
                        int rowsAffected = updatePxxtCmd.ExecuteNonQuery();

                        string message = $"update pxxt_code ICE data_type='110' code='1' sqlcode:[0] rec:[{rowsAffected}] ";
                        Console.WriteLine(message);
                        _logger.Info(message);
                    }
                }
            }
            finally
            {
                _oraConn.Close();
            }
        }

        private static void UpdateMark560()
        {
            _oraConn.Open();
            try
            {
                string[] hospIds = GetHospIds();
                int hospIdCount = hospIds.Length;

                Console.WriteLine($"    1 - UpdateMark560()-  select mhat_hospbsc _execFlag:[{_execFlag}] _inputFeeYm:[{_inputFeeYm}] _inputHospId(選項):[{_inputHospId}]  ");
                Console.WriteLine($"        hospIdCount:[{hospIdCount}] sqlcode:[0] rec:[{hospIdCount}] ");

                foreach (string hospId in hospIds)
                {
                    string message = $"    update hosp_id:[{hospId}] ";
                    Console.Write(message);
                    _logger.Info(message);

                    StringBuilder strSQL = new StringBuilder();
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        strSQL.AppendLine("   SET MARK_560 = 'Y'");
                        strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                        cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                        strSQL.AppendLine("   AND FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                        cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                        strSQL.AppendLine("   AND MARK_560 IS NULL");
                        strSQL.AppendLine("   AND ORDER_CODE IN (");
                        strSQL.AppendLine("       SELECT CODE FROM PXXT_CODE");
                        strSQL.AppendLine("        WHERE SUB_SYS = 'PBA' AND DATA_TYPE = '560'");
                        strSQL.AppendLine("        UNION");
                        strSQL.AppendLine("       SELECT '64164B' CODE FROM DUAL UNION");
                        strSQL.AppendLine("       SELECT '64169B' CODE FROM DUAL UNION");
                        strSQL.AppendLine("       SELECT '64202B' CODE FROM DUAL UNION");
                        strSQL.AppendLine("       SELECT '64170B' CODE FROM DUAL UNION");
                        strSQL.AppendLine("       SELECT '64162B' CODE FROM DUAL UNION");
                        strSQL.AppendLine("       SELECT '64258B' CODE FROM DUAL UNION");
                        strSQL.AppendLine("       SELECT '64201B' CODE FROM DUAL)");

                        cmd.CommandText = strSQL.ToString();
                        int rowsAffected = cmd.ExecuteNonQuery();

                        message = $" sqlcode:[0] rec:[{rowsAffected}] ";
                        Console.WriteLine(message);
                        _logger.Info(message);
                    }
                }
            }
            finally
            {
                _oraConn.Close();
            }
        }

        private static void UpdateMark24hrPBA560()
        {
            int loopCount = 0;

            Console.WriteLine($"    0 - UpdateMark24hrPBA560() - select pxxt_code _execFlag:[{_execFlag}] _inputHospId:[{_inputHospId}] _inputFeeYm:[{_inputFeeYm}] ");

            _oraConn.Open();
            try
            {
                StringBuilder strSQL = new StringBuilder();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("SELECT CODE ORDER_CODE");
                    strSQL.AppendLine("  FROM PXXT_CODE");
                    strSQL.AppendLine(" WHERE SUB_SYS = 'PBA'");
                    strSQL.AppendLine("   AND DATA_TYPE = '560'");
                    strSQL.AppendLine("   AND VALID_S_DATE = TO_DATE(:validSDate,'YYYYMMDD')");
                    cmd.Parameters.Add(new OracleParameter("validSDate", _validSDate));

                    cmd.CommandText = strSQL.ToString();
                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string orderCode = reader["ORDER_CODE"].ToString();

                            string message = $"    update s_mark_24hr:[{_mark24hr}] _inputHospId:[{_inputHospId}] _inputFeeYm:[{_inputFeeYm}] orderCode:[{orderCode}] _branchCode:[{_branchCode}] ";
                            Console.WriteLine(message);
                            _logger.Info(message);

                            StringBuilder updateSQL = new StringBuilder();
                            using (OracleCommand updateCmd = _oraConn.CreateCommand())
                            {
                                updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL A");
                                updateSQL.AppendLine("   SET MARK_24HR = :mark24hr");
                                updateCmd.Parameters.Add(new OracleParameter("mark24hr", _mark24hr));
                                updateSQL.AppendLine(" WHERE (A.HOSP_ID = :inputHospId OR UPPER(:inputHospId2) = 'ALL' OR NVL(:inputHospId3,0) = 0)");
                                updateCmd.Parameters.Add(new OracleParameter("inputHospId", _inputHospId));
                                updateCmd.Parameters.Add(new OracleParameter("inputHospId2", _inputHospId));
                                updateCmd.Parameters.Add(new OracleParameter("inputHospId3", _inputHospId));
                                updateSQL.AppendLine("   AND A.FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                                updateCmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                                updateSQL.AppendLine("   AND A.ORDER_CODE = :orderCode");
                                updateCmd.Parameters.Add(new OracleParameter("orderCode", orderCode));
                                updateSQL.AppendLine("   AND A.BRANCH_CODE = (CASE WHEN :branchCode BETWEEN '1' AND '6' THEN :branchCode2 ELSE A.BRANCH_CODE END)");
                                updateCmd.Parameters.Add(new OracleParameter("branchCode", _branchCode));
                                updateCmd.Parameters.Add(new OracleParameter("branchCode2", _branchCode));
                                updateSQL.AppendLine("   AND A.MARK_24HR = '5'");
                                updateSQL.AppendLine("   AND TO_DATE(SUBSTR(A.INIT_WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD') < LAST_DAY(ADD_MONTHS(A.FEE_YM,1))+1");

                                updateCmd.CommandText = updateSQL.ToString();
                                int rowsAffected = updateCmd.ExecuteNonQuery();

                                message = $"    sqlcode:[0] rec:[{rowsAffected}] ";
                                Console.WriteLine(message);
                                _logger.Info(message);

                                loopCount++;
                            }
                        }
                    }
                }

                string updateMessage = $"    update RAPI_ASSAY_DATA_FINAL loopCount:[{loopCount}] ";
                Console.WriteLine(updateMessage);
                _logger.Info(updateMessage);
            }
            finally
            {
                _oraConn.Close();
            }
        }

        private static void UpdateMark24hrHPA()
        {
            _oraConn.Open();
            try
            {
                string[] hospIds = GetHospIds();
                int hospIdCount = hospIds.Length;

                Console.WriteLine($"    1 - UpdateMark24hrHPA()-  select mhat_hospbsc _execFlag:[{_execFlag}] _inputFeeYm:[{_inputFeeYm}] _inputHospId(選項):[{_inputHospId}]  ");
                Console.WriteLine($"        hospIdCount:[{hospIdCount}] sqlcode:[0] rec:[{hospIdCount}] ");

                foreach (string hospId in hospIds)
                {
                    string message = $"    update hosp_id:[{hospId}] ";
                    Console.Write(message);
                    _logger.Info(message);

                    StringBuilder strSQL = new StringBuilder();
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        strSQL.AppendLine("   SET MARK_24HR = 'A'");
                        strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                        cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                        strSQL.AppendLine("   AND FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                        cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                        strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        strSQL.AppendLine("   AND MARK_24HR = '5'");
                        strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                        strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK = '3')");
                        strSQL.AppendLine("   AND REAL_RECV_DATE IS NULL");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(CASE_TIME,1,8),'YYYYMMDD') > TO_DATE(SUBSTR(NVL(OUT_DATE,APPL_E_DATE),1,8),'YYYYMMDD')");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-TO_DATE(SUBSTR(CASE_TIME,1,8),'YYYYMMDD') <= 2");

                        cmd.CommandText = strSQL.ToString();
                        int rowsAffected = cmd.ExecuteNonQuery();

                        message = $" sqlcode:[0] rec:[{rowsAffected}] ";
                        Console.WriteLine(message);
                        _logger.Info(message);
                    }
                }
            }
            finally
            {
                _oraConn.Close();
            }
        }

        private static void UpdateMark24hrHPAA()
        {
            _oraConn.Open();
            try
            {
                string[] hospIds = GetHospIds();
                int hospIdCount = hospIds.Length;

                Console.WriteLine($"    1 - UpdateMark24hrHPAA()-  select mhat_hospbsc _execFlag:[{_execFlag}] _inputFeeYm:[{_inputFeeYm}] _inputHospId(選項):[{_inputHospId}]  ");
                Console.WriteLine($"        hospIdCount:[{hospIdCount}] sqlcode:[0] rec:[{hospIdCount}] ");

                foreach (string hospId in hospIds)
                {
                    string message = $"    update hosp_id:[{hospId}] ";
                    Console.Write(message);
                    _logger.Info(message);

                    StringBuilder strSQL = new StringBuilder();
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        strSQL.AppendLine("   SET MARK_24HR = 'A'");
                        strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                        cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                        strSQL.AppendLine("   AND FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                        cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                        strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        strSQL.AppendLine("   AND MARK_24HR = '5'");
                        strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                        strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK = '3')");
                        strSQL.AppendLine("   AND REAL_RECV_DATE IS NOT NULL");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(REAL_RECV_DATE,1,8),'YYYYMMDD') > TO_DATE(SUBSTR(NVL(OUT_DATE,APPL_E_DATE),1,8),'YYYYMMDD')");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-TO_DATE(SUBSTR(REAL_RECV_DATE,1,8),'YYYYMMDD') <= 2");

                        cmd.CommandText = strSQL.ToString();
                        int rowsAffected = cmd.ExecuteNonQuery();

                        message = $" sqlcode:[0] rec:[{rowsAffected}] ";
                        Console.WriteLine(message);
                        _logger.Info(message);
                    }
                }
            }
            finally
            {
                _oraConn.Close();
            }
        }

        private static void UpdateMark24hrHPB()
        {
            _oraConn.Open();
            try
            {
                string[] hospIds = GetHospIds();
                int hospIdCount = hospIds.Length;

                Console.WriteLine($"    1 - UpdateMark24hrHPB()-  select mhat_hospbsc _execFlag:[{_execFlag}] _inputFeeYm:[{_inputFeeYm}] _inputHospId(選項):[{_inputHospId}]  ");
                Console.WriteLine($"        hospIdCount:[{hospIdCount}] sqlcode:[0] rec:[{hospIdCount}] ");

                foreach (string hospId in hospIds)
                {
                    string message = $"    update hosp_id:[{hospId}] ";
                    Console.Write(message);
                    _logger.Info(message);

                    StringBuilder strSQL = new StringBuilder();
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        strSQL.AppendLine("   SET MARK_24HR = 'B'");
                        strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                        cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                        strSQL.AppendLine("   AND FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                        cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                        strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        strSQL.AppendLine("   AND MARK_24HR = '5'");
                        strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                        strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK = '3')");
                        strSQL.AppendLine("   AND REAL_RECV_DATE IS NULL");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(CASE_TIME,1,8),'YYYYMMDD') < TO_DATE(SUBSTR(IN_DATE,1,8),'YYYYMMDD')");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-TO_DATE(SUBSTR(NVL(OUT_DATE,APPL_E_DATE),1,8),'YYYYMMDD') <= 2");

                        cmd.CommandText = strSQL.ToString();
                        int rowsAffected = cmd.ExecuteNonQuery();

                        message = $" sqlcode:[0] rec:[{rowsAffected}] ";
                        Console.WriteLine(message);
                        _logger.Info(message);
                    }
                }
            }
            finally
            {
                _oraConn.Close();
            }
        }

        private static void UpdateMark24hrHPBB()
        {
            _oraConn.Open();
            try
            {
                string[] hospIds = GetHospIds();
                int hospIdCount = hospIds.Length;

                Console.WriteLine($"    1 - UpdateMark24hrHPBB()-  select mhat_hospbsc _execFlag:[{_execFlag}] _inputFeeYm:[{_inputFeeYm}] _inputHospId(選項):[{_inputHospId}]  ");
                Console.WriteLine($"        hospIdCount:[{hospIdCount}] sqlcode:[0] rec:[{hospIdCount}] ");

                foreach (string hospId in hospIds)
                {
                    string message = $"    update hosp_id:[{hospId}] ";
                    Console.Write(message);
                    _logger.Info(message);

                    StringBuilder strSQL = new StringBuilder();
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        strSQL.AppendLine("   SET MARK_24HR = 'B'");
                        strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                        cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                        strSQL.AppendLine("   AND FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                        cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                        strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        strSQL.AppendLine("   AND MARK_24HR = '5'");
                        strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                        strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK = '3')");
                        strSQL.AppendLine("   AND REAL_RECV_DATE IS NOT NULL");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(REAL_RECV_DATE,1,8),'YYYYMMDD') < TO_DATE(SUBSTR(IN_DATE,1,8),'YYYYMMDD')");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-TO_DATE(SUBSTR(NVL(OUT_DATE,APPL_E_DATE),1,8),'YYYYMMDD') <= 2");

                        cmd.CommandText = strSQL.ToString();
                        int rowsAffected = cmd.ExecuteNonQuery();

                        message = $" sqlcode:[0] rec:[{rowsAffected}] ";
                        Console.WriteLine(message);
                        _logger.Info(message);
                    }
                }
            }
            finally
            {
                _oraConn.Close();
            }
        }

        private static void UpdateMark24hrHPC()
        {
            _oraConn.Open();
            try
            {
                string[] hospIds = GetHospIds();
                int hospIdCount = hospIds.Length;

                Console.WriteLine($"    1 - UpdateMark24hrHPC()-  select mhat_hospbsc _execFlag:[{_execFlag}] _inputFeeYm:[{_inputFeeYm}] _inputHospId(選項):[{_inputHospId}]  ");
                Console.WriteLine($"        hospIdCount:[{hospIdCount}] sqlcode:[0] rec:[{hospIdCount}] ");

                foreach (string hospId in hospIds)
                {
                    string message = $"    update hosp_id:[{hospId}] ";
                    Console.Write(message);
                    _logger.Info(message);

                    StringBuilder strSQL = new StringBuilder();
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        strSQL.AppendLine("   SET MARK_24HR = 'C'");
                        strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                        cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                        strSQL.AppendLine("   AND FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                        cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                        strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        strSQL.AppendLine("   AND MARK_24HR = '5'");
                        strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                        strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK = '3')");
                        strSQL.AppendLine("   AND REAL_RECV_DATE IS NULL");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(CASE_TIME,1,8),'YYYYMMDD') BETWEEN TO_DATE(SUBSTR(IN_DATE,1,8),'YYYYMMDD') AND TO_DATE(SUBSTR(NVL(OUT_DATE,APPL_E_DATE),1,8),'YYYYMMDD')");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-TO_DATE(SUBSTR(NVL(OUT_DATE,APPL_E_DATE),1,8),'YYYYMMDD') <= 2");

                        cmd.CommandText = strSQL.ToString();
                        int rowsAffected = cmd.ExecuteNonQuery();

                        message = $" sqlcode:[0] rec:[{rowsAffected}] ";
                        Console.WriteLine(message);
                        _logger.Info(message);
                    }
                }
            }
            finally
            {
                _oraConn.Close();
            }
        }

        private static void UpdateMark24hrHPCC()
        {
            _oraConn.Open();
            try
            {
                string[] hospIds = GetHospIds();
                int hospIdCount = hospIds.Length;

                Console.WriteLine($"    1 - UpdateMark24hrHPCC()-  select mhat_hospbsc _execFlag:[{_execFlag}] _inputFeeYm:[{_inputFeeYm}] _inputHospId(選項):[{_inputHospId}]  ");
                Console.WriteLine($"        hospIdCount:[{hospIdCount}] sqlcode:[0] rec:[{hospIdCount}] ");

                foreach (string hospId in hospIds)
                {
                    string message = $"    update hosp_id:[{hospId}] ";
                    Console.Write(message);
                    _logger.Info(message);

                    StringBuilder strSQL = new StringBuilder();
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        strSQL.AppendLine("   SET MARK_24HR = 'C'");
                        strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                        cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                        strSQL.AppendLine("   AND FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                        cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                        strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        strSQL.AppendLine("   AND MARK_24HR = '5'");
                        strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                        strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK = '3')");
                        strSQL.AppendLine("   AND REAL_RECV_DATE IS NOT NULL");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(REAL_RECV_DATE,1,8),'YYYYMMDD') BETWEEN TO_DATE(SUBSTR(IN_DATE,1,8),'YYYYMMDD') AND TO_DATE(SUBSTR(NVL(OUT_DATE,APPL_E_DATE),1,8),'YYYYMMDD')");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-TO_DATE(SUBSTR(NVL(OUT_DATE,APPL_E_DATE),1,8),'YYYYMMDD') <= 2");

                        cmd.CommandText = strSQL.ToString();
                        int rowsAffected = cmd.ExecuteNonQuery();

                        message = $" sqlcode:[0] rec:[{rowsAffected}] ";
                        Console.WriteLine(message);
                        _logger.Info(message);
                    }
                }
            }
            finally
            {
                _oraConn.Close();
            }
        }

        private static void UpdateMark24hrHPClear()
        {
            _oraConn.Open();
            try
            {
                StringBuilder strSQL = new StringBuilder();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("SELECT HOSP_ID");
                    strSQL.AppendLine("  FROM MHAT_HOSPBSC A");
                    strSQL.AppendLine(" WHERE EXISTS (SELECT 1");
                    strSQL.AppendLine("                FROM RAPI_ASSAY_DATA_FINAL B");
                    strSQL.AppendLine("               WHERE A.HOSP_ID = B.HOSP_ID AND B.MARK_24HR IN ('A','B','C','D','E')");
                    strSQL.AppendLine("                 AND B.FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                    cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                    strSQL.AppendLine("                 AND (HOSP_ID = :inputHospId OR");
                    cmd.Parameters.Add(new OracleParameter("inputHospId", _inputHospId));
                    strSQL.AppendLine("                      'ALL' = UPPER(:inputHospId2) OR");
                    cmd.Parameters.Add(new OracleParameter("inputHospId2", _inputHospId));
                    strSQL.AppendLine("                      NVL(:inputHospId3,0) = 0))");
                    cmd.Parameters.Add(new OracleParameter("inputHospId3", _inputHospId));
                    strSQL.AppendLine(" ORDER BY HOSP_ID ASC");

                    cmd.CommandText = strSQL.ToString();
                    DataTable dt = new DataTable();
                    using (OracleDataAdapter adapter = new OracleDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }

                    int hospIdCount = dt.Rows.Count;
                    Console.WriteLine($"    1 - UpdateMark24hrHPClear()-  select mhat_hospbsc _execFlag:[{_execFlag}] _inputFeeYm:[{_inputFeeYm}] _inputHospId(選項):[{_inputHospId}]  ");
                    Console.WriteLine($"        hospIdCount:[{hospIdCount}] sqlcode:[0] rec:[{hospIdCount}] ");

                    foreach (DataRow row in dt.Rows)
                    {
                        string hospId = row["HOSP_ID"].ToString();
                        string message = $"    update hosp_id:[{hospId}] ";
                        Console.Write(message);
                        _logger.Info(message);

                        StringBuilder updateSQL = new StringBuilder();
                        using (OracleCommand updateCmd = _oraConn.CreateCommand())
                        {
                            updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                            updateSQL.AppendLine("   SET MARK_24HR = '5'");
                            updateSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                            updateCmd.Parameters.Add(new OracleParameter("hospId", hospId));
                            updateSQL.AppendLine("   AND FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                            updateCmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                            updateSQL.AppendLine("   AND MARK_24HR IN ('A','B','C','D','E')");

                            updateCmd.CommandText = updateSQL.ToString();
                            int rowsAffected = updateCmd.ExecuteNonQuery();

                            message = $" sqlcode:[0] rec:[{rowsAffected}] ";
                            Console.WriteLine(message);
                            _logger.Info(message);
                        }
                    }
                }
            }
            finally
            {
                _oraConn.Close();
            }
        }

        private static void UpdateMark24hrHPD()
        {
            _oraConn.Open();
            try
            {
                string[] hospIds = GetHospIds();
                int hospIdCount = hospIds.Length;

                Console.WriteLine($"    1 - UpdateMark24hrHPD()-  select mhat_hospbsc _execFlag:[{_execFlag}] _inputFeeYm:[{_inputFeeYm}] _inputHospId(選項):[{_inputHospId}]  ");
                Console.WriteLine($"        hospIdCount:[{hospIdCount}] sqlcode:[0] rec:[{hospIdCount}] ");

                foreach (string hospId in hospIds)
                {
                    string message = $"    update hosp_id:[{hospId}] ";
                    Console.Write(message);
                    _logger.Info(message);

                    StringBuilder strSQL = new StringBuilder();
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        strSQL.AppendLine("   SET MARK_24HR = 'D'");
                        strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                        cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                        strSQL.AppendLine("   AND FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                        cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                        strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        strSQL.AppendLine("   AND MARK_24HR = '5'");
                        strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                        strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK = '3')");
                        strSQL.AppendLine("   AND (RECARD_MARK = '2' AND TO_DATE(SUBSTR(TREAT_DT,1,8),'YYYYMMDD') > TO_DATE(SUBSTR(CASE_TIME,1,8),'YYYYMMDD'))");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(TREAT_DT,1,8),'YYYYMMDD') > TO_DATE(SUBSTR(NVL(OUT_DATE,APPL_E_DATE),1,8),'YYYYMMDD')");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-TO_DATE(SUBSTR(TREAT_DT,1,8),'YYYYMMDD') <= 2");

                        cmd.CommandText = strSQL.ToString();
                        int rowsAffected = cmd.ExecuteNonQuery();

                        message = $" sqlcode:[0] rec:[{rowsAffected}] ";
                        Console.WriteLine(message);
                        _logger.Info(message);
                    }
                }
            }
            finally
            {
                _oraConn.Close();
            }
        }

        private static void UpdateMark24hrHPE()
        {
            _oraConn.Open();
            try
            {
                string[] hospIds = GetHospIds();
                int hospIdCount = hospIds.Length;

                Console.WriteLine($"    1 - UpdateMark24hrHPE()-  select mhat_hospbsc _execFlag:[{_execFlag}] _inputFeeYm:[{_inputFeeYm}] _inputHospId(選項):[{_inputHospId}]  ");
                Console.WriteLine($"        hospIdCount:[{hospIdCount}] sqlcode:[0] rec:[{hospIdCount}] ");

                foreach (string hospId in hospIds)
                {
                    string message = $"    update hosp_id:[{hospId}] ";
                    Console.Write(message);
                    _logger.Info(message);

                    StringBuilder strSQL = new StringBuilder();
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        strSQL.AppendLine("   SET MARK_24HR = 'E'");
                        strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                        cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                        strSQL.AppendLine("   AND FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                        cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                        strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        strSQL.AppendLine("   AND MARK_24HR = '5'");
                        strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                        strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK = '3')");
                        strSQL.AppendLine("   AND (RECARD_MARK = '2' AND TO_DATE(SUBSTR(TREAT_DT,1,8),'YYYYMMDD') > TO_DATE(SUBSTR(CASE_TIME,1,8),'YYYYMMDD'))");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(TREAT_DT,1,8),'YYYYMMDD') BETWEEN TO_DATE(SUBSTR(IN_DATE,1,8),'YYYYMMDD') AND TO_DATE(SUBSTR(NVL(OUT_DATE,APPL_E_DATE),1,8),'YYYYMMDD')");
                        strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-TO_DATE(SUBSTR(NVL(OUT_DATE,APPL_E_DATE),1,8),'YYYYMMDD') <= 2");

                        cmd.CommandText = strSQL.ToString();
                        int rowsAffected = cmd.ExecuteNonQuery();

                        message = $" sqlcode:[0] rec:[{rowsAffected}] ";
                        Console.WriteLine(message);
                        _logger.Info(message);
                    }
                }
            }
            finally
            {
                _oraConn.Close();
            }
        }

        private static void UpdateIceiAssayDl6MstReset()
        {
            _oraConn.Open();
            try
            {
                StringBuilder strSQL = new StringBuilder();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("SELECT HOSP_ID");
                    strSQL.AppendLine("  FROM MHAT_HOSPBSC A");
                    strSQL.AppendLine(" WHERE EXISTS (SELECT 1");
                    strSQL.AppendLine("                FROM RAPI_ASSAY_DATA_FINAL B");
                    strSQL.AppendLine("               WHERE A.HOSP_ID = B.HOSP_ID AND B.MARK_24HR IN ('A','B','C','D','E')");
                    strSQL.AppendLine("                 AND B.FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                    cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                    strSQL.AppendLine("                 AND (HOSP_ID = :inputHospId OR");
                    cmd.Parameters.Add(new OracleParameter("inputHospId", _inputHospId));
                    strSQL.AppendLine("                      'ALL' = UPPER(:inputHospId2) OR");
                    cmd.Parameters.Add(new OracleParameter("inputHospId2", _inputHospId));
                    strSQL.AppendLine("                      NVL(:inputHospId3,0) = 0))");
                    cmd.Parameters.Add(new OracleParameter("inputHospId3", _inputHospId));
                    strSQL.AppendLine(" ORDER BY HOSP_ID ASC");

                    cmd.CommandText = strSQL.ToString();
                    DataTable dt = new DataTable();
                    using (OracleDataAdapter adapter = new OracleDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }

                    int hospIdCount = dt.Rows.Count;
                    Console.WriteLine($"    1 - UpdateIceiAssayDl6MstReset()-  select mhat_hospbsc _execFlag:[{_execFlag}] _inputFeeYm:[{_inputFeeYm}] _inputHospId(選項):[{_inputHospId}]  ");
                    Console.WriteLine($"        hospIdCount:[{hospIdCount}] sqlcode:[0] rec:[{hospIdCount}] ");

                    foreach (DataRow row in dt.Rows)
                    {
                        string hospId = row["HOSP_ID"].ToString();
                        string message = $"    update hosp_id:[{hospId}] ";
                        Console.Write(message);
                        _logger.Info(message);

                        StringBuilder updateSQL = new StringBuilder();
                        using (OracleCommand updateCmd = _oraConn.CreateCommand())
                        {
                            updateSQL.AppendLine("UPDATE ICEI_ASSAY_DL6_MST SET ASSAY_STATUS = NULL");
                            updateSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                            updateCmd.Parameters.Add(new OracleParameter("hospId", hospId));
                            updateSQL.AppendLine("   AND FEE_YM >= TO_DATE(:inputFeeYm,'YYYYMMDD')");
                            updateCmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                            updateSQL.AppendLine("   AND FEE_YM <= ADD_MONTHS(TO_DATE(:inputFeeYm2,'YYYYMMDD'),2)");
                            updateCmd.Parameters.Add(new OracleParameter("inputFeeYm2", _inputFeeYm));

                            updateCmd.CommandText = updateSQL.ToString();
                            int rowsAffected = updateCmd.ExecuteNonQuery();

                            message = $" sqlcode:[0] rec:[{rowsAffected}] ";
                            Console.WriteLine(message);
                            _logger.Info(message);
                        }
                    }
                }
            }
            finally
            {
                _oraConn.Close();
            }
        }
        #endregion

        #region Helper Methods
        private static string[] GetHospIds()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT HOSP_ID");
                strSQL.AppendLine("  FROM MHAT_HOSPBSC A");
                strSQL.AppendLine(" WHERE EXISTS (SELECT 1");
                strSQL.AppendLine("                FROM RAPI_ASSAY_DATA_FINAL B");
                strSQL.AppendLine("               WHERE A.HOSP_ID = B.HOSP_ID");
                strSQL.AppendLine("                 AND B.FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                strSQL.AppendLine("                 AND (HOSP_ID = :inputHospId OR");
                cmd.Parameters.Add(new OracleParameter("inputHospId", _inputHospId));
                strSQL.AppendLine("                      'ALL' = UPPER(:inputHospId2) OR");
                cmd.Parameters.Add(new OracleParameter("inputHospId2", _inputHospId));
                strSQL.AppendLine("                      NVL(:inputHospId3,0) = 0))");
                cmd.Parameters.Add(new OracleParameter("inputHospId3", _inputHospId));
                strSQL.AppendLine(" ORDER BY HOSP_ID ASC");

                cmd.CommandText = strSQL.ToString();
                DataTable dt = new DataTable();
                using (OracleDataAdapter adapter = new OracleDataAdapter(cmd))
                {
                    adapter.Fill(dt);
                }

                string[] hospIds = new string[dt.Rows.Count];
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    hospIds[i] = dt.Rows[i]["HOSP_ID"].ToString();
                }
                return hospIds;
            }
        }
        #endregion
    }
}
```