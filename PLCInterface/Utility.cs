using Oracle.ManagedDataAccess.Client;
using System;
using System.IO;

namespace PLCInterface
{
    public static class Utility
    {
        public static void SendOracleDB(string PART_NO, string LINE_CD, string TEST_DT, string OK_YN, string IMAGEPATH, string CAMERA_NO)
        {
            try
            {
                /*Oracle 데이타베이스를 연결하기 위해서는 OracleConnection 클래스를 사용한다.
                 * Connection 클래스를 생성할 때는 Connection String을 넣어 주어야 하는데, Oracle Connection String을 지정하는 방법은 여러가지 방식이 있다.
                 * 가장 기본적으로 Data Source, User Id, Password등을 지정해 줄 수 있는데, Data Source에는 tnsnames.ora에 지정된 net service name을 지정하고, User Id, Password에는 Oracle 사용자명과 암호를 지정한다.
                 * (tnsnames.ora에 net service name이 있더라도 tnsnames.ora 자체를 못 찾는 경우가 있는데, 이때는 TNS_ADMIN 환경변수를 설정해 본다)
                 * net service name를 사용하지 않고 직접 네트워크 연결정보를 넣어서 사용할 수도 있다.*/
                //string strConn = "Data Source=net_service_name;User Id=xx;Password=xx;";
                //string strConn = $"Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST={GlobalInfo.OracleIP})(PORT={GlobalInfo.OraclePort})))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME={GlobalInfo.OracleServiceName})));User Id={GlobalInfo.OracleUserID};Password={GlobalInfo.OraclePassword};";
                string strConn = $"Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST={GlobalInfo.OracleIP})(PORT={GlobalInfo.OraclePort})))(CONNECT_DATA=(SERVICE_NAME={GlobalInfo.OracleServiceName})));User Id={GlobalInfo.OracleUserID};Password={GlobalInfo.OraclePassword};";
                // 오라클 연결
                OracleConnection conn = new OracleConnection(strConn);
                conn.Open();

                // 명령 객체 생성
                OracleCommand cmd = new OracleCommand();
                cmd.Connection = conn;

                if (IMAGEPATH == string.Empty || IMAGEPATH == "")
                    IMAGEPATH = "NotDefined";

                // SQL문 지정 및 INSERT 실행
                cmd.CommandText = $"INSERT INTO {GlobalInfo.OracleTable} (PART_NO, LINE_CD, TEST_DT, OK_YN, IMAGEPATH, CAMERA_NO) VALUES ('{PART_NO}', '{GlobalInfo.OracleColumn_LINE_CD}', '{TEST_DT}', '{OK_YN}', '{IMAGEPATH}', '{CAMERA_NO}')";
                Logger.Debug($"{cmd.CommandText}");
                cmd.ExecuteNonQuery();

                conn.Close();
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
        }

        public static void MakeFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}