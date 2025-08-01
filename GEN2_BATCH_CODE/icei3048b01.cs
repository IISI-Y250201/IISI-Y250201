```csharp
/*******************************************************************
程式代碼：icei3048b01
程式名稱：上傳概況日報表統計作業
功能簡述：統計上傳概況日報表資料
參    數：
參數一：程式代號 分區別
範例一：icei3048b01 1
讀取檔案：無
異動檔案：無
作    者：系統轉換
歷次修改時間：
1.20230101
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

namespace icei3048b01
{
    public class icei3048b01
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

        #region 參數與變數
        private class Argu
        {
            public string branchCode { get; set; } = string.Empty;
            public string treatDateS { get; set; } = string.Empty;
            public string treatDateE { get; set; } = string.Empty;
        }
        private static Argu _argu = new Argu();

        private class Sql200
        {
            public string hospId { get; set; } = string.Empty;
            public string treatD { get; set; } = string.Empty;
            public long qty { get; set; }
        }
        private static Sql200 _sql200 = new Sql200();

        private static string _wkExeDate = string.Empty;
        private static string _wkExeDateYmd = string.Empty;
        private static string _wkExeDateYmdS = string.Empty;
        #endregion

        /* ---------- Main ---------- */
        static void Main(string[] args)
        {
            try
            {
                // PXX_start_process_m
                MEDM_SysLib.MEDM_StartProcess();

                CheckArg(args);

                // 取得系統日期
                DateTime sysdate = DateTime.Now;
                _wkExeDate = sysdate.ToString("yyyyMMdd");
                _wkExeDateYmd = (sysdate.Year - 1911).ToString() + sysdate.ToString("MMdd");

                // 計算93天前的日期 (民國年)
                _wkExeDateYmdS = (sysdate.AddDays(-93).Year - 1911).ToString() + sysdate.AddDays(-93).ToString("MMdd");

                Console.WriteLine($"wk_exe_date_ymd_s<{_wkExeDateYmdS}>");
                Console.WriteLine($"wk_exe_date_ymd<{_wkExeDateYmd}>");

                // 處理資料
                ProcessHospitalData();

                _proList.exitCode = 0;
                _proList.message = "程式 icei3048b01 結束";
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
                // PXX_exit_process_m
                MEDM_SysLib.MEDM_ExitProcess(_proList.exitCode, _proList.message);
            }
        }

        /* ---------- parameter check ---------- */
        // Original: check_arg()
        private static void CheckArg(string[] args)
        {
            if (args.Length != 1)
            {
                _proList.exitCode = -99;
                _proList.message = "參數：參數個數 輸入錯誤";
                
                // 顯示使用說明
                var prog = AppDomain.CurrentDomain.FriendlyName;
                Console.WriteLine($"用法：{prog} 分區別");
                Console.WriteLine($"範例：{prog} 1");
                Console.WriteLine($"範例：{prog} 2");
                
                _logger.Error(_proList.message);
                throw new ArgumentException(_proList.message);
            }

            // 分區別必為1碼
            _argu.branchCode = args[0];
            if (_argu.branchCode != "1" && _argu.branchCode != "2" && _argu.branchCode != "3" &&
                _argu.branchCode != "4" && _argu.branchCode != "5" && _argu.branchCode != "6")
            {
                _proList.exitCode = -2;
                _proList.message = "參數：分區別須為1,2,3,4,5,6";
                
                // 顯示使用說明
                var prog = AppDomain.CurrentDomain.FriendlyName;
                Console.WriteLine($"用法：{prog} 分區別");
                Console.WriteLine($"範例：{prog} 1");
                Console.WriteLine($"範例：{prog} 2");
                
                _logger.Error(_proList.message);
                throw new ArgumentException(_proList.message);
            }

            Console.WriteLine("參數：參數檢查正確");
            _logger.Info("參數：參數檢查正確");

            Console.WriteLine($"branch_code<{_argu.branchCode}>");
            _logger.Info($"branch_code<{_argu.branchCode}>");
        }

        // 處理醫院資料
        private static void ProcessHospitalData()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT B.hosp_id, B.treat_d, B.qty");
                strSQL.AppendLine("FROM (");
                strSQL.AppendLine("  SELECT hosp_id hosp_id, treat_d treat_d, COUNT(*) qty");
                strSQL.AppendLine("  FROM ICEI_PBIT_ORD");
                strSQL.AppendLine("  WHERE branch_code = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _argu.branchCode));
                strSQL.AppendLine("  AND treat_d >= :wkExeDateYmdS");
                cmd.Parameters.Add(new OracleParameter("wkExeDateYmdS", _wkExeDateYmdS));
                strSQL.AppendLine("  GROUP BY hosp_id, treat_d");
                strSQL.AppendLine(") B");
                strSQL.AppendLine("ORDER BY B.hosp_id, B.treat_d, B.qty");

                cmd.CommandText = strSQL.ToString();
                _oraConn.Open();

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _sql200.hospId = reader["hosp_id"].ToString();
                        _sql200.treatD = reader["treat_d"].ToString();
                        _sql200.qty = Convert.ToInt64(reader["qty"]);

                        string msg = $"branch_code<{_argu.branchCode}> hosp_id<{_sql200.hospId}>,treat_d<{_sql200.treatD}>,qty<{_sql200.qty}>";
                        Console.WriteLine(msg);
                        _logger.Info(msg);

                        // 處理每一筆醫院資料
                        ProcessHospitalRecord();
                    }
                }
                _oraConn.Close();
            }
        }

        // 處理單筆醫院記錄
        private static void ProcessHospitalRecord()
        {
            // 刪除現有資料
            DeleteExistingData();

            // 插入A類型資料
            InsertTypeAData();

            // 插入I類型資料
            InsertTypeIData();

            // 提交交易
            CommitTransaction();

            // 刪除IDC資料
            DeleteIdcData();

            // 插入IDC資料
            InsertIdcData();

            // 提交交易
            CommitTransaction();
        }

        // 刪除現有資料
        private static void DeleteExistingData()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("DELETE ICEI_DAY_REPORT_STS");
                strSQL.AppendLine("WHERE branch_code = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _argu.branchCode));
                strSQL.AppendLine("AND hosp_id = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _sql200.hospId));
                strSQL.AppendLine("AND treat_d = TO_DATE(SUBSTR(:treatD,1,3)+1911 || SUBSTR(:treatD,4,4), 'YYYYMMDD')");
                cmd.Parameters.Add(new OracleParameter("treatD", _sql200.treatD));

                cmd.CommandText = strSQL.ToString();
                cmd.ExecuteNonQuery();
            }
        }

        // 插入A類型資料
        private static void InsertTypeAData()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("INSERT INTO ICEI_DAY_REPORT_STS(");
                strSQL.AppendLine("  hosp_cnt_type, branch_code, hosp_id, treat_d,");
                strSQL.AppendLine("  order_code_type, code, code_cname,");
                strSQL.AppendLine("  ORDER_CODE, cure_cname,");
                strSQL.AppendLine("  order_qty, audit_qty,");
                strSQL.AppendLine("  self_upload_qty, oth_upload_qty,");
                strSQL.AppendLine("  LACK_RPT_QTY, LACK_IMG_QTY, txt_date");
                strSQL.AppendLine(")");
                strSQL.AppendLine("SELECT hosp_cnt_type, branch_code, hosp_id,");
                strSQL.AppendLine("  TO_DATE(SUBSTR(treat_d,1,3)+1911 || SUBSTR(treat_d,4,4), 'YYYYMMDD') treat_d,");
                strSQL.AppendLine("  order_code_type,");
                strSQL.AppendLine("  a_code code,");
                strSQL.AppendLine("  a_code_cname code_cname,");
                strSQL.AppendLine("  order_code, cure_cname,");
                strSQL.AppendLine("  SUM(NVL(order_qty,0)) order_qty,");
                strSQL.AppendLine("  SUM(NVL(a_audit_qty,0)) audit_qty,");
                strSQL.AppendLine("  SUM(CASE WHEN a_upload_mark = '1' THEN NVL(A_AUDIT_QTY,0) ELSE 0 END) self_upload_qty,");
                strSQL.AppendLine("  SUM(CASE WHEN a_upload_mark = '2' THEN NVL(A_AUDIT_QTY,0) ELSE 0 END) oth_upload_qty,");
                strSQL.AppendLine("  NULL LACK_RPT_QTY,");
                strSQL.AppendLine("  NULL LACK_IMG_QTY,");
                strSQL.AppendLine("  SYSDATE txt_date");
                strSQL.AppendLine("FROM icei_pbit_ord");
                strSQL.AppendLine("WHERE branch_code = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _argu.branchCode));
                strSQL.AppendLine("AND hosp_id = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _sql200.hospId));
                strSQL.AppendLine("AND treat_d = :treatD");
                cmd.Parameters.Add(new OracleParameter("treatD", _sql200.treatD));
                strSQL.AppendLine("AND order_code_type = 'A'");
                strSQL.AppendLine("GROUP BY hosp_cnt_type, branch_code, hosp_id,");
                strSQL.AppendLine("  TO_DATE(SUBSTR(treat_d,1,3)+1911 || SUBSTR(treat_d,4,4), 'YYYYMMDD'),");
                strSQL.AppendLine("  order_code_type, a_code, a_code_cname, order_code, cure_cname");

                cmd.CommandText = strSQL.ToString();
                cmd.ExecuteNonQuery();
            }
        }

        // 插入I類型資料
        private static void InsertTypeIData()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("INSERT INTO ICEI_DAY_REPORT_STS(");
                strSQL.AppendLine("  hosp_cnt_type, branch_code, hosp_id, treat_d,");
                strSQL.AppendLine("  order_code_type, code, code_cname,");
                strSQL.AppendLine("  ORDER_CODE, cure_cname,");
                strSQL.AppendLine("  order_qty, audit_qty,");
                strSQL.AppendLine("  self_upload_qty, oth_upload_qty,");
                strSQL.AppendLine("  LACK_RPT_QTY, LACK_IMG_QTY, txt_date");
                strSQL.AppendLine(")");
                strSQL.AppendLine("SELECT hosp_cnt_type, branch_code, hosp_id,");
                strSQL.AppendLine("  TO_DATE(SUBSTR(treat_d,1,3)+1911 || SUBSTR(treat_d,4,4), 'YYYYMMDD') treat_d,");
                strSQL.AppendLine("  order_code_type,");
                strSQL.AppendLine("  i_code code,");
                strSQL.AppendLine("  i_code_cname code_cname,");
                strSQL.AppendLine("  order_code, cure_cname,");
                strSQL.AppendLine("  SUM(NVL(order_qty,0)) order_qty,");
                strSQL.AppendLine("  SUM(CASE WHEN i_upload_mark = '1' AND order_code IN ('34004C','01271C','23506C','01272C','01273C','00315C','00316C','00317C','34006B','34005B')");
                strSQL.AppendLine("           THEN NVL(order_QTY,0)");
                strSQL.AppendLine("           WHEN (i_upload_mark = '1' OR a_upload_mark = '1' OR a_upload_mark = '2' OR i_upload_mark = '2') AND");
                strSQL.AppendLine("                order_code NOT IN ('34004C','01271C','23506C','01272C','01273C','00315C','00316C','00317C','34006B','34005B')");
                strSQL.AppendLine("           THEN NVL(order_QTY,0)");
                strSQL.AppendLine("           ELSE 0 END) audit_qty,");
                strSQL.AppendLine("  SUM(CASE WHEN i_upload_mark = '2' OR a_upload_mark = '2'");
                strSQL.AppendLine("           THEN 0");
                strSQL.AppendLine("           WHEN i_upload_mark = '1'");
                strSQL.AppendLine("           THEN NVL(order_QTY,0)");
                strSQL.AppendLine("           WHEN a_upload_mark = '1'");
                strSQL.AppendLine("           THEN NVL(order_QTY,0)");
                strSQL.AppendLine("           ELSE 0 END) self_upload_qty,");
                strSQL.AppendLine("  SUM(CASE WHEN i_upload_mark = '2' OR a_upload_mark = '2'");
                strSQL.AppendLine("           THEN NVL(order_QTY,0)");
                strSQL.AppendLine("           ELSE 0 END) oth_upload_qty,");
                strSQL.AppendLine("  SUM(CASE WHEN NVL(a_audit_qty,0) = 0 AND NVL(i_audit_qty,0) > 0 AND");
                strSQL.AppendLine("                order_code NOT IN ('34004C','01271C','23506C','01272C','01273C','00315C','00316C','00317C','34006B','34005B')");
                strSQL.AppendLine("           THEN NVL(order_QTY,0) ELSE 0 END) LACK_RPT_QTY,");
                strSQL.AppendLine("  SUM(CASE WHEN NVL(a_audit_qty,0) > 0 AND");
                strSQL.AppendLine("                NVL(i_audit_qty,0) = 0");
                strSQL.AppendLine("           THEN NVL(order_QTY,0) ELSE 0 END) LACK_IMG_QTY,");
                strSQL.AppendLine("  SYSDATE txt_date");
                strSQL.AppendLine("FROM icei_pbit_ord a");
                strSQL.AppendLine("WHERE branch_code = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _argu.branchCode));
                strSQL.AppendLine("AND hosp_id = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _sql200.hospId));
                strSQL.AppendLine("AND treat_d = :treatD");
                cmd.Parameters.Add(new OracleParameter("treatD", _sql200.treatD));
                strSQL.AppendLine("AND order_code_type = 'I'");
                strSQL.AppendLine("GROUP BY hosp_cnt_type, branch_code, hosp_id,");
                strSQL.AppendLine("  TO_DATE(SUBSTR(treat_d,1,3)+1911 || SUBSTR(treat_d,4,4), 'YYYYMMDD'),");
                strSQL.AppendLine("  order_code_type, i_code, i_code_cname, order_code, cure_cname");

                cmd.CommandText = strSQL.ToString();
                cmd.ExecuteNonQuery();
            }
        }

        // 提交交易
        private static void CommitTransaction()
        {
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = "COMMIT";
                cmd.ExecuteNonQuery();
            }
        }

        // 刪除IDC資料
        private static void DeleteIdcData()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("DELETE nhi_idc.ICEE_DAY_REPORT_STS");
                strSQL.AppendLine("WHERE branch_code = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _argu.branchCode));
                strSQL.AppendLine("AND hosp_id = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _sql200.hospId));
                strSQL.AppendLine("AND treat_d = TO_DATE(SUBSTR(:treatD,1,3)+1911 || SUBSTR(:treatD,4,4), 'YYYYMMDD')");
                cmd.Parameters.Add(new OracleParameter("treatD", _sql200.treatD));

                cmd.CommandText = strSQL.ToString();
                cmd.ExecuteNonQuery();
            }
        }

        // 插入IDC資料
        private static void InsertIdcData()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("INSERT INTO nhi_idc.ICEE_DAY_REPORT_STS (");
                strSQL.AppendLine("  hosp_cnt_type, branch_code, hosp_id, treat_d,");
                strSQL.AppendLine("  order_code_type, code, code_cname,");
                strSQL.AppendLine("  ORDER_CODE, cure_cname,");
                strSQL.AppendLine("  order_qty, audit_qty,");
                strSQL.AppendLine("  self_upload_qty, oth_upload_qty,");
                strSQL.AppendLine("  LACK_RPT_QTY, LACK_IMG_QTY, txt_date");
                strSQL.AppendLine(")");
                strSQL.AppendLine("SELECT hosp_cnt_type, branch_code, hosp_id, treat_d,");
                strSQL.AppendLine("  order_code_type, code, code_cname,");
                strSQL.AppendLine("  ORDER_CODE, cure_cname,");
                strSQL.AppendLine("  order_qty, audit_qty,");
                strSQL.AppendLine("  self_upload_qty, oth_upload_qty,");
                strSQL.AppendLine("  LACK_RPT_QTY, LACK_IMG_QTY, txt_date");
                strSQL.AppendLine("FROM ICEI_DAY_REPORT_STS");
                strSQL.AppendLine("WHERE branch_code = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _argu.branchCode));
                strSQL.AppendLine("AND hosp_id = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _sql200.hospId));
                strSQL.AppendLine("AND treat_d = TO_DATE(SUBSTR(:treatD,1,3)+1911 || SUBSTR(:treatD,4,4), 'YYYYMMDD')");
                cmd.Parameters.Add(new OracleParameter("treatD", _sql200.treatD));

                cmd.CommandText = strSQL.ToString();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
```