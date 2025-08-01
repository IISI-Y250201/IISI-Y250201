```csharp
/*******************************************************************
程式代碼：icei2037b01
程式名稱：ICE 清檔作業
功能簡述：清除過期的ICE相關資料表資料
參    數：
參數一：Table代碼(選項)
範例一：icei2037b01 1 3501200000
讀取檔案：無
異動檔案：無
作    者：系統轉換
歷次修改時間：
1.2023/12/01
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

namespace icei2037b01
{
    public class icei2037b01
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
        private static int _tableId = 0;
        private static int _delDl1 = 0;
        private static int _delDl2 = 0;
        private static int _delDl3 = 0;
        private static int _delDl4 = 0;
        private static int _delDl5 = 0;
        private static int _delDl7 = 0;
        private static int _delDl1To7 = 0;
        private static int _del60 = 0;
        private static int _del61 = 0;
        private static string _inputHospId = string.Empty;
        private static string _feeYm = string.Empty;
        private static string _feeYm9 = string.Empty;
        private static string _treatD = string.Empty;
        #endregion

        static void Main(string[] args)
        {
            try
            {
                // Original: PXX_start_process();
                MEDM_SysLib.MEDM_StartProcess();

                WriteDate();
                Console.WriteLine("===================================");

                CheckArg(args);

                _feeYm = string.Empty;
                _feeYm9 = string.Empty;
                _treatD = string.Empty;

                // Replace SQL query with C# DateTime calculations
                DateTime currentDate = DateTime.Now;
                DateTime firstDayOfMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
                DateTime minus14Months = firstDayOfMonth.AddMonths(-14);
                DateTime minus9Months = firstDayOfMonth.AddMonths(-9);
                
                _feeYm = minus14Months.ToString("yyyyMMdd");
                _treatD = (minus14Months.Year - 1911).ToString() + minus14Months.AddDays(DateTime.DaysInMonth(minus14Months.Year, minus14Months.Month) - 1).ToString("MMdd");
                _feeYm9 = minus9Months.ToString("yyyyMMdd");

                _logger.Info($"\n _feeYm:[{_feeYm}] _treatD:[{_treatD}] _feeYm9:[{_feeYm9}]");

                if (_del60 == 1)
                {
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        cmd.CommandText = "BEGIN ICEI0001.SP_DROP_ICEI_60_OLD_PARTITIONS; END;";
                        int recordsAffected = cmd.ExecuteNonQuery();
                        Console.WriteLine($"\nSP_DROP_ICEI_60_OLD_PARTITIONS sqlcode:[0] rec:[{recordsAffected}]");
                    }
                }

                if (_del61 == 1)
                {
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        cmd.CommandText = "BEGIN ICEI0001.SP_DROP_ICEI_61_OLD_PARTITIONS; END;";
                        int recordsAffected = cmd.ExecuteNonQuery();
                        Console.WriteLine($"\nSP_DROP_ICEI_61_OLD_PARTITIONS sqlcode:[0] rec:[{recordsAffected}]");
                    }
                }

                if (_delDl1To7 == 1)
                {
                    StringBuilder strSQL = new StringBuilder();
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        strSQL.AppendLine("SELECT hosp_id FROM mhat_hospbsc WHERE hosp_id = NVL(:inputHospId, hosp_id)");
                        cmd.Parameters.Add(new OracleParameter("inputHospId", _inputHospId));

                        _logger.Info($"\n strSQL:[\n{strSQL}\n]");
                        cmd.CommandText = strSQL.ToString();

                        using (OracleDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string hospId = reader.GetString(0);

                                if (_delDl1 == 1)
                                {
                                    DeleteRecords(hospId, "nhi_idc.icee_assay_dl1_dtl", "fee_ym", _delDl1);
                                }

                                if (_delDl2 == 1)
                                {
                                    DeleteRecords(hospId, "nhi_idc.icee_assay_dl2_dtl", "fee_ym", _delDl2);
                                }

                                if (_delDl3 == 1)
                                {
                                    DeleteRecords(hospId, "nhi_idc.icee_assay_dl3_dtl", "fee_ym", _delDl3);
                                }

                                if (_delDl4 == 1)
                                {
                                    DeleteRecords(hospId, "nhi_idc.icee_ctmri_dl4_dtl", "fee_ym", _delDl4);
                                }

                                if (_delDl5 == 1)
                                {
                                    DeleteRecords(hospId, "nhi_idc.icee_assay_dl5_dtl", "fee_ym", _delDl5);
                                }

                                if (_delDl7 == 1)
                                {
                                    DeleteDl7Records(hospId);
                                }
                            }
                        }
                    }
                }

                Console.WriteLine("===================================");
                WriteDate();

                _proList.exitCode = 0;
                _proList.message = "正常結束";
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
                // Original: PXX_exit_process(0, 0, "正常結束");
                MEDM_SysLib.MEDM_ExitProcess(_proList.exitCode, _proList.message);
            }
        }

        // Original: check_arg()
        private static void CheckArg(string[] args)
        {
            _tableId = 0;
            _delDl1 = 0;
            _delDl2 = 0;
            _delDl3 = 0;
            _delDl4 = 0;
            _delDl5 = 0;
            _delDl7 = 0;
            _delDl1To7 = 0;
            _del60 = 0;
            _del61 = 0;

            if (args.Length == 0)
            {
                _delDl1 = 1;
                _delDl2 = 1;
                _delDl3 = 1;
                _delDl4 = 1;
                _delDl5 = 1;
                _delDl7 = 1;
                _delDl1To7 = 1;
                _del60 = 1;
                _del61 = 1;
            }
            else if (args.Length == 1 || args.Length == 2)
            {
                _tableId = int.Parse(args[0]);

                switch (_tableId)
                {
                    case 0:
                        _delDl1 = 1;
                        _delDl2 = 1;
                        _delDl3 = 1;
                        _delDl4 = 1;
                        _delDl5 = 1;
                        _delDl7 = 1;
                        _delDl1To7 = 1;
                        _del60 = 1;
                        _del61 = 1;
                        break;
                    case 1:
                        _delDl1 = 1;
                        _delDl1To7 = 1;
                        break;
                    case 2:
                        _delDl2 = 1;
                        _delDl1To7 = 1;
                        break;
                    case 3:
                        _delDl3 = 1;
                        _delDl1To7 = 1;
                        break;
                    case 4:
                        _delDl4 = 1;
                        _delDl1To7 = 1;
                        break;
                    case 5:
                        _delDl5 = 1;
                        _delDl1To7 = 1;
                        break;
                    case 7:
                        _delDl7 = 1;
                        _delDl1To7 = 1;
                        break;
                    case 60:
                        _del60 = 1;
                        break;
                    case 61:
                        _del61 = 1;
                        break;
                    default:
                        ShowUsage();
                        break;
                }

                if (args.Length == 2)
                {
                    _inputHospId = args[1];
                    Console.WriteLine($"_inputHospId :[{_inputHospId}]");
                }
            }
            else
            {
                ShowUsage();
            }

            Console.WriteLine($"_tableId :[{_tableId}]");
            Console.WriteLine($"_delDl1  :[{_delDl1}]");
            Console.WriteLine($"_delDl2  :[{_delDl2}]");
            Console.WriteLine($"_delDl3  :[{_delDl3}]");
            Console.WriteLine($"_delDl4  :[{_delDl4}]");
            Console.WriteLine($"_delDl5  :[{_delDl5}]");
            Console.WriteLine($"_delDl7  :[{_delDl7}]");
            Console.WriteLine($"_del60   :[{_del60}]");
            Console.WriteLine($"_del61   :[{_del61}]");
        }

        private static void ShowUsage()
        {
            var prog = AppDomain.CurrentDomain.FriendlyName;
            Console.WriteLine("參數種類：程式代號  Table代碼(選項) 院所代碼(選項)");
            Console.WriteLine();
            Console.WriteLine("參數1   ：Table代碼 (選項)");
            Console.WriteLine("           0 : icee_assay_dl1_dtl ~ icei_assay_61_dtl");
            Console.WriteLine("           1 : icee_assay_dl1_dtl");
            Console.WriteLine("           2 : icee_assay_dl2_dtl");
            Console.WriteLine("           3 : icee_assay_dl3_dtl");
            Console.WriteLine("           4 : icee_ctmri_dl4_dtl");
            Console.WriteLine("           5 : icee_assay_dl5_dtl");
            Console.WriteLine("           7 : icee_home_dl7_dtl");
            Console.WriteLine("          60 : icei_assay_60_dtl");
            Console.WriteLine("          61 : icei_assay_61_dtl");
            Console.WriteLine();
            Console.WriteLine("參數2   ： 院所代碼(選項)");
            Console.WriteLine();
            Console.WriteLine($"範例1   ：{prog}");
            Console.WriteLine($"範例2   ：{prog} 1");
            Console.WriteLine($"範例3   ：{prog} 1 3501200000");

            _proList.exitCode = 10;
            _proList.message = "參數錯誤";
            throw new ArgumentException();
        }

        private static void WriteDate()
        {
            string dateTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            WriteMsg($"sysdate:[{dateTime}]");
        }

        private static void WriteMsg(string message)
        {
            string formattedMsg = $"\n {message}";
            Console.WriteLine(formattedMsg);
            _logger.Info(formattedMsg);
        }

        private static void DeleteRecords(string hospId, string tableName, string dateColumn, int delFlag)
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine($"DELETE FROM {tableName}");
                strSQL.AppendLine($"WHERE hosp_id = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                strSQL.AppendLine($"AND {dateColumn} <= ADD_MONTHS(TRUNC(SYSDATE, 'MM'), -14)");

                cmd.CommandText = strSQL.ToString();
                int recordsAffected = cmd.ExecuteNonQuery();
                Console.WriteLine($"hospId:[{hospId}] delFlag:[{delFlag}] sqlcode:[0] rec:[{recordsAffected}]");

                cmd.CommandText = "COMMIT";
                cmd.ExecuteNonQuery();
            }
        }

        private static void DeleteDl7Records(string hospId)
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("DELETE FROM nhi_idc.icee_home_dl7_dtl");
                strSQL.AppendLine("WHERE hosp_id = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                strSQL.AppendLine("AND treat_d <= :treatD");
                cmd.Parameters.Add(new OracleParameter("treatD", _treatD));

                cmd.CommandText = strSQL.ToString();
                int recordsAffected = cmd.ExecuteNonQuery();
                Console.WriteLine($"hospId:[{hospId}] _delDl7:[{_delDl7}] _treatD:[{_treatD}] sqlcode:[0] rec:[{recordsAffected}]");

                cmd.CommandText = "COMMIT";
                cmd.ExecuteNonQuery();
            }
        }
    }
}
```