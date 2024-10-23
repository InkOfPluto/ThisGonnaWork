using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;

public class UDP_Comms : MonoBehaviour
{
    private Socket sock;

    [SerializeField]
    private string IpAddress = "192.168.1.66";
    [SerializeField]
    private int portNum = 5660;


    IPAddress address;  //= IPAddress.Parse(IpAddress);

    //[SerializeField]
    //private String sendNum = "0";

    // Start is called before the first frame update
    void Start()
    {
        address  = IPAddress.Parse(IpAddress);

        sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        Debug.Log("Sender: Socket made\n");
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void SendMsgtoWrist(float val)
    {
        Send(address, portNum, val.ToString());
    }


    void Send(IPAddress ip, int port, String msg)
    {

        IPAddress serverAddr = ip;
        IPEndPoint endPoint = new IPEndPoint(serverAddr, port);
        String toSend = msg;
        byte[] send_buffer = Encoding.ASCII.GetBytes(msg);

        sock.SendTo(send_buffer, endPoint);
    }
}
