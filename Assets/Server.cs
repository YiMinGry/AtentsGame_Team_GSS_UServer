using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MySql.Data.MySqlClient;

public class ServerClient
{
    public TcpClient tcp;

    public ServerClient(TcpClient clientSocket)
    {
        tcp = clientSocket;
    }
}

public class Server : MonoBehaviour
{
    public struct userData
    {
        public string idx;
        public string ssID;
        public string ID;
        public string nickName;
        public string coin1;
        public string coin2;
    }

    public InputField portField;
    public Text serverIP;

    public Text serverlog;
    List<ServerClient> clients;
    List<ServerClient> disconnectList;

    TcpListener server;
    bool serverStarted;

    private void Awake()
    {
        String strHostName = string.Empty;
        IPHostEntry ipEntry = Dns.GetHostEntry(Dns.GetHostName());
        IPAddress[] addr = ipEntry.AddressList;

        serverIP.text = ($"IP Address : {addr[1].ToString()} ");
    }
    public void SetLog(string msg)
    {
        serverlog.text = serverlog.text + msg + "\n";
    }

    public void ServerCreate()
    {
        _connectionAddress = string.Format("Server={0};Port={1};Database={2};Uid={3};Pwd={4}", _server, _port, _database, _id, _pw);
        SetLog("DB세팅..");


        clients = new List<ServerClient>();
        disconnectList = new List<ServerClient>();

        try
        {
            int _port = int.Parse(portField.text);

            server = new TcpListener(IPAddress.Any, _port);
            server.Start();

            StartListening();
            serverStarted = true;
            SetLog("서버시작");
        }
        catch (Exception e)
        {
            SetLog(e.Message);
        }
    }

    void Update()
    {
        if (!serverStarted) return;

        foreach (ServerClient c in clients)
        {
            // 클라이언트가 여전히 연결되있나?
            if (!IsConnected(c.tcp))
            {
                c.tcp.Close();
                disconnectList.Add(c);
                continue;
            }
            // 클라이언트로부터 체크 메시지를 받는다
            else
            {
                NetworkStream s = c.tcp.GetStream();
                if (s.DataAvailable)
                {
                    string data = new StreamReader(s).ReadLine();
                    if (data != null)
                    {
                        OnMessage(c, data);
                    }
                }
            }
        }

        for (int i = 0; i < disconnectList.Count - 1; i++)
        {
            Broadcast($"{disconnectList[i].tcp} 연결이 끊어졌습니다");

            clients.Remove(disconnectList[i]);
            disconnectList.RemoveAt(i);
        }
    }

    bool IsConnected(TcpClient c)
    {
        try
        {
            if (c != null && c.Client != null && c.Client.Connected)
            {
                if (c.Client.Poll(0, SelectMode.SelectRead))
                {
                    return !(c.Client.Receive(new byte[1], SocketFlags.Peek) == 0);
                }

                return true;
            }
            else
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    void StartListening()
    {
        server.BeginAcceptTcpClient(AcceptTcpClient, server);
    }

    void AcceptTcpClient(IAsyncResult ar)
    {
        TcpListener listener = (TcpListener)ar.AsyncState;
        clients.Add(new ServerClient(listener.EndAcceptTcpClient(ar)));
        StartListening();
    }

    protected void OnMessage(ServerClient c, string data)
    {
        var text = data;

        SetLog("server recv : " + text + "\n");
        JObject json2 = new JObject();
        json2 = JObject.Parse(text);

        string _cmd = json2["cmd"].ToString();

        switch (_cmd)
        {
            case "ssEnter":
                {
                    string _ssid = Guid.NewGuid().ToString();
                    if (FindUserInfo(json2["ID"].ToString()) == true)
                    { //기존 유저인지 체크

                        SetLog("기존 유저 접속 " + json2["ID"].ToString() + "\n");
                        UserSSIDUpdate(json2["ID"].ToString(), _ssid);

                    }
                    else
                    { //신규 유저일경우
                      //유저 디비에 추가하고
                        SetLog("신규 유저 접속 " + json2["ID"].ToString() + "\n");

                        json2.Add("ssID", _ssid);
                        UserInsert(json2);

                    }


                    //닉네임 설정여부 체크 
                    if (CheckUserNickName(json2["ID"].ToString()) == false)
                    {//닉네임 설정 안됨 //클라한테 닉네임 설정하라고 패킷 보내줘야함

                        JObject SetUserNickNameData = new JObject();
                        SetUserNickNameData.Add("cmd", "SetUserNickName");
                        SetUserNickNameData.Add("retMsg", "닉네임을 설정해주세요");
                        SetUserNickNameData.Add("ssID", _ssid);
                        SetUserNickNameData.Add("ID", json2["ID"].ToString());

                        Send(SetUserNickNameData.ToString(), c);

                        return;
                    }


                    //닉네임까지 이상 없을경우 유저 데이터 클라로 넘겨주기
                    userData _info = new userData();
                    _info = GetUserInfo(json2["ID"].ToString());

                    JObject _userData = new JObject();
                    _userData.Add("cmd", "LoginOK");
                    _userData.Add("retMsg", "로그인에 성공했습니다.");
                    _userData.Add("idx", _info.idx);
                    _userData.Add("ssID", _info.ssID);
                    _userData.Add("ID", _info.ID);
                    _userData.Add("nickName", _info.nickName);
                    _userData.Add("coin1", _info.coin1);
                    _userData.Add("coin2", _info.coin2);
                    SetLog("기존 유저 접속 " + _userData.ToString() + "\n");

                    Send(_userData.ToString(), c);

                }
                break;
            case "SetUserNickName":
                {
                    if (FindUserInfo(json2["ID"].ToString()) == true)
                    {
                        UserNinameUpdate(json2["ID"].ToString(), json2["nickName"].ToString());
                    }

                    if (CheckUserNickName(json2["ID"].ToString()) == true)
                    {
                        //닉네임까지 이상 없을경우 유저 데이터 클라로 넘겨주기
                        userData _info = new userData();
                        _info = GetUserInfo(json2["ID"].ToString());

                        JObject _userData = new JObject();
                        _userData.Add("cmd", "LoginOK");
                        _userData.Add("retMsg", "로그인에 성공했습니다.");
                        _userData.Add("idx", _info.idx);
                        _userData.Add("ssID", _info.ssID);
                        _userData.Add("ID", _info.ID);
                        _userData.Add("nickName", _info.nickName);
                        _userData.Add("coin1", _info.coin1);
                        _userData.Add("coin2", _info.coin2);
                        SetLog("기존 유저 접속 " + _userData.ToString() + "\n");

                        Send(_userData.ToString(), c);
                    }
                }
                break;
            case "TotalRanking":
                {
                    JObject _nCmd = new JObject();
                    _nCmd.Add("cmd", "TotalRanking");
                    JArray rktmp = TotalRank();
                    _nCmd.Add("Top10", rktmp);
                    Send(_nCmd.ToString(), c);
                }
                break;
            case "ReadRanking":
                {
                    JObject _nCmd = new JObject();
                    _nCmd.Add("cmd", "ReadRanking");
                    JArray rktmp = GetMG1TopTRank(json2["MG_NAME"].ToString());
                    _nCmd.Add("Top10", rktmp);
                    Send(_nCmd.ToString(), c);
                }

                break;
            case "UpdateRanking":
                {
                    if (!(GetMG1MyScore(json2["MG_NAME"].ToString(), json2["ID"].ToString()).Count > 0))
                    {
                        MGRankInsert(json2["MG_NAME"].ToString(), json2);
                    }

                    //랭킹 점수 업데이트
                    MGRankUpdate(json2["MG_NAME"].ToString(), json2);


                    JObject _nCmd = new JObject();
                    _nCmd.Add("cmd", "UpdateRanking");

                    JArray allRankArr = GetMG1TopTRank(json2["MG_NAME"].ToString());
                    JObject myRkData = GetMG1MyScore(json2["MG_NAME"].ToString(), json2["ID"].ToString());
                    int rankIdx = -1;

                    for (int i = 0; i < allRankArr.Count; i++)
                    {
                        if (allRankArr[i]["ID"].ToString().Equals(json2["ID"].ToString()))
                        {
                            rankIdx = i + 1;
                        }
                    }

                    myRkData.Add("ranking", rankIdx);

                    _nCmd.Add("allRankArr", allRankArr);
                    _nCmd.Add("myRkData", myRkData);


                    Send(_nCmd.ToString(), c);
                }
                break;
            case "Chat":
                {
                    Broadcast(json2.ToString());
                }
                break;
            case "원하는기능":
                {

                }
                break;
        }
    }


    void Broadcast(string data)
    {
        foreach (var c in clients)
        {
            try
            {
                StreamWriter writer = new StreamWriter(c.tcp.GetStream());
                string str = data;
                str = str.Replace("\r\n", "");
                writer.WriteLine(str);
                writer.Flush();
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }
        }
    }
    void Send(string data, ServerClient cl)
    {

        try
        {
            StreamWriter writer = new StreamWriter(cl.tcp.GetStream());
            string str = data;
            str = str.Replace("\r\n", "");
            writer.WriteLine(str);
            writer.Flush();
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }

    }



    /*------------db---------------------- */



    string _server = "localhost";
    int _port = 3306;
    string _database = "userinfo";
    string _infoTable = "info";
    string _id = "root";
    string _pw = "root";
    string _connectionAddress = "";

    //커맨드 리턴
    public MySqlCommand GetCommand(string _query)
    {
        MySqlConnection conn = new MySqlConnection(_connectionAddress);
        conn.Open();

        MySqlCommand cmd = new MySqlCommand();
        cmd.Connection = conn;
        cmd.CommandText = _query;
        cmd.ExecuteNonQuery();
        return cmd;
    }
    //데이터리더 리턴
    public MySqlDataReader GetDataReader(string _query)
    {
        MySqlConnection conn = new MySqlConnection(_connectionAddress);
        conn.Open();

        MySqlCommand cmd = new MySqlCommand();
        cmd.Connection = conn;
        cmd.CommandText = _query;
        MySqlDataReader rdr = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);

        return rdr;
    }
    //유저찾기
    private bool FindUserInfo(string _id)
    {
        bool ret = false;

        try
        {
            string _query = string.Format($"SELECT * FROM {_infoTable}");

            MySqlDataReader table = GetDataReader(_query);

            while (table.Read())
            {
                if (_id.Equals(table["ID"].ToString()))
                {
                    ret = true;
                    break;
                }
            }

            table.Close();
        }
        catch (Exception exc)
        {
            SetLog("FindUserInfo !!!!!" + exc.Message);
        }

        return ret;

    }
    //유저 데이터 받아오기
    private userData GetUserInfo(string _id)
    {
        userData _info = new userData();

        _info.idx = "";

        try
        {
            string _query = string.Format($"SELECT * FROM {_infoTable}");

            MySqlDataReader table = GetDataReader(_query);

            while (table.Read())
            {
                if (_id.Equals(table["ID"].ToString()))
                {
                    _info.idx = table["idx"].ToString();
                    _info.ssID = table["ssID"].ToString();
                    _info.ID = table["ID"].ToString();
                    _info.nickName = table["nickName"].ToString();
                    _info.coin1 = table["coin1"].ToString();
                    _info.coin2 = table["coin2"].ToString();

                    return _info;
                }
            }

            table.Close();

        }
        catch (Exception exc)
        {
            SetLog("GetUserInfo !!!!!" + exc.Message);
        }

        return _info;

    }
    // 유저생성
    public bool UserInsert(JObject _data)
    {
        bool ret = false;

        if (_data["ID"].ToString() == "")
        {
            ret = false;

            SetLog("아이디 공백!!!!!");
        }
        else
        {
            try
            {
                string _query = string.Format($"INSERT IGNORE INTO {_infoTable} (ssID, ID, nickName,coin1, coin2) VALUES ('{_data["ssID"].ToString()}','{_data["ID"].ToString()}','{""}','{1000}','{10}');");
                MySqlCommand command = GetCommand(_query);

                ret = true;
            }
            catch (Exception exc)
            {

                SetLog("UserInsert !!!!!" + exc.Message);
                ret = false;
            }
        }
        return ret;

    }
    // 유저닉네임 체크
    private bool CheckUserNickName(string _id)
    {

        bool ret = false;
        try
        {
            string _query = string.Format($"SELECT * FROM {_infoTable}");

            MySqlDataReader table = GetDataReader(_query);

            while (table.Read())
            {
                if (_id.Equals(table["ID"].ToString()) && table["nickName"].ToString() != "")
                {
                    ret = true;
                    break;
                }
            }

            table.Close();
        }
        catch (Exception exc)
        {
            SetLog("CheckUserNickName !!!!!" + exc.Message);
        }

        return ret;
    }
    //닉네임 수정
    private void UserNinameUpdate(string _id, string _nName)
    {
        try
        {
            string _query = string.Format($"UPDATE {_infoTable} SET nickName='{_nName}' WHERE ID='{_id}';");
            MySqlCommand command = GetCommand(_query);


        }
        catch (Exception exc)
        {
            SetLog("UserNinameUpdate !!!!!" + exc.Message);
        }
    }
    //세션 업데이트
    private void UserSSIDUpdate(string _id, string _ssID)
    {
        try
        {
            string _query = string.Format($"UPDATE {_infoTable} SET ssID='{_ssID}' WHERE ID='{_id}';");

            MySqlCommand command = GetCommand(_query);
        }
        catch (Exception exc)
        {
            SetLog("UserSSIDUpdate !!!!!" + exc.Message);
        }

    }


    JArray TotalRank()
    {
        JArray ret = new JArray();

        try
        {
            string _query = string.Format($"select ID, nickName, MG_1_Score + MG_2_Score+ MG_3_Score+ MG_4_Score+ MG_5_Score as MG_Total_Score, dense_rank() over (order by  MG_1_Score + MG_2_Score+ MG_3_Score+ MG_4_Score+ MG_5_Score desc) as MG_Total_Rank from ranktable;");

            MySqlDataReader table = GetDataReader(_query);


            JArray rankArr = new JArray();

            JObject cmdTmp = new JObject();

            while (table.Read())
            {
                if (rankArr.Count < 10)//최대 열개만 나오게
                {
                    JObject rkData = new JObject();
                    rkData.Add("ID", table["ID"].ToString());
                    rkData.Add("nickName", table["nickName"].ToString());
                    rkData.Add("MG_Total_Score", table["MG_Total_Score"].ToString());
                    rkData.Add("MG_Total_Rank", table["MG_Total_Rank"].ToString());

                    rankArr.Add(rkData);
                }
            }

            ret = rankArr;

            table.Close();
        }
        catch (Exception exc)
        {
            SetLog("TotalRank !!!!!" + exc.Message);
        }

        return ret;
    }

    //미니게임 랭킹 가져오기
    private JArray GetMG1TopTRank(string _gameName)
    {

        JArray ret = new JArray();

        try
        {
            string _query = string.Format($"select ID, nickName, {_gameName}_Score, dense_rank() over (order by {_gameName}_Score desc) as {_gameName}_Rank from ranktable;");

            MySqlDataReader table = GetDataReader(_query);

            JArray rankArr = new JArray();

            JObject cmdTmp = new JObject();

            while (table.Read())
            {
                if (rankArr.Count < 10)//최대 열개만 나오게
                {
                    JObject rkData = new JObject();
                    rkData.Add("ID", table["ID"].ToString());
                    rkData.Add("nickName", table["nickName"].ToString());
                    rkData.Add($"Score", table[$"{_gameName}_Score"].ToString());
                    rkData.Add($"Rank", table[$"{_gameName}_Rank"].ToString());

                    rankArr.Add(rkData);
                }
            }

            ret = rankArr;

            table.Close();
        }
        catch (Exception exc)
        {
            SetLog("GetMG1TopTRank !!!!!" + exc.Message);
        }

        return ret;
    }
    //내 미니게임 점수 가져오기
    private JObject GetMG1MyScore(string _gameName, string _id)
    {

        JObject ret = new JObject();

        try
        {
            string _query = string.Format($"SELECT * FROM ranktable;");
            MySqlDataReader table = GetDataReader(_query);

            while (table.Read())
            {
                if (_id.Equals(table["ID"].ToString()))
                {
                    ret.Add("msg", "요청한 유저의 랭킹");
                    ret.Add("ID", table["ID"].ToString());
                    ret.Add("nickName", table["nickName"].ToString());
                    ret.Add($"Score", table[$"{_gameName}_Score"].ToString());
                    break;
                }
            }

            table.Close();
        }
        catch (Exception exc)
        {
            SetLog("GetMG1MyRank !!!!!" + exc.Message);
        }

        return ret;
    }
    //미니게임 랭킹 추가
    public void MGRankInsert(string _gameName, JObject _data)
    {
        try
        {
            string _query = string.Format($"INSERT IGNORE INTO ranktable (ID, nickName, MG_1_Score, MG_2_Score, MG_3_Score, MG_4_Score, MG_5_Score) VALUES ('{_data["ID"].ToString()}','{_data["nickName"].ToString()}','0','0','0','0','0');");

            MySqlCommand command = GetCommand(_query);
        }
        catch (Exception exc)
        {

            SetLog("MGRankInsert !!!!!" + exc.Message);
        }

    }
    //미니게임 랭킹 업데이트
    private void MGRankUpdate(string _gameName, JObject _data)
    {
        try
        {
            string _query = string.Format($"UPDATE ranktable SET {_gameName}_Score='{_data["Score"].ToString()}' WHERE ID='{_data["ID"].ToString()}';");

            MySqlCommand command = GetCommand(_query);
        }
        catch (Exception exc)
        {
            SetLog("MGRankUpdate !!!!!" + exc.Message);
        }
    }
}