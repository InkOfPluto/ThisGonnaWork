using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using UnityEngine;

public class MotorTest : MonoBehaviour
{
    [Header("Ports | 端口")]
    public string Comport1 = "COM5";
    public string Comport2 = "COM6";
    // Start is called before the first frame update

    private SerialPort serial1;
    private SerialPort serial2;
    private void Start()
    {

        try
        {
            serial1 = new SerialPort(Comport1, 115200);
            serial2 = new SerialPort(Comport2, 115200);
            serial1.Open();
            serial2.Open();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("串口连接失败: " + e.Message);
        }
    }

    // Update is called once per frame
    private void Update()
    {
        //if (Input.GetKeyUp(KeyCode.Q))
        //{
        //    serial1.WriteLine("fgfgfgfgfgfgfgfgfgfgfgfgbnbnbnbnbnbnbnbnbnbnbnbn");
        //}
        //if (Input.GetKeyUp(KeyCode.E))
        //{
        //    serial2.WriteLine("fgfgfgfgfgfgfgfgfgfgfgfgbnbnbnbnbnbnbnbnbnbnbnbn");
        //}
        if (Input.GetKeyUp(KeyCode.Z))
        {
            serial1.WriteLine("fffffffffffffffffffffff");//Serial1 Thumb f COUNTERCLOCK UP 相对于手指向上滑（右边看 逆时针）
        }
        if (Input.GetKeyUp(KeyCode.X))
        {
            serial1.WriteLine("bbbbbbbbbbbbbbbbbbbbb");  //Serail1 Thumb b clock down 相对于手指向下滑（右边看 顺时针）
        }
        if (Input.GetKeyUp(KeyCode.C))
        {
            serial1.WriteLine("gggggggggggggggggggggg");    //Serail1 Ring g clock down 相对于手指向下滑（右边看 顺时针）
        }
        if (Input.GetKeyUp(KeyCode.V))
        {
            serial1.WriteLine("nnnnnnnnnnnnnnnnnnnnnnn");   //Serial1 Ring n COUNTERCLOCK UP 相对于手指向上滑（右边看 逆时针）
        }
        if (Input.GetKeyUp(KeyCode.B))
        {
            serial2.WriteLine("ffffffffffffffffffff");    //Serail2 Index f COUNTERCLOCK UP 相对于手指向下滑（右边看 顺时针）
        }
        if (Input.GetKeyUp(KeyCode.N))
        {
            serial2.WriteLine("bbbbbbbbbbbbbbbbbbbbb");   //Serail2 Index b clock down 相对于手指向下滑（右边看 顺时针）
        }
        if (Input.GetKeyUp(KeyCode.D))
        {
            serial2.WriteLine("gggggggggggggggggggggg");    //Serial2 Mid g COUNTERCLOCK UP 相对于手指向上滑（右边看 逆时针）
        }
        if (Input.GetKeyUp(KeyCode.F))
        {
            serial2.WriteLine("nnnnnnnnnnnnnnnnnnnnnnn");   //Serail2 Mid n clock down 相对于手指向下滑（右边看 顺时针）
        }
    }
}

