using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Data.OleDb;

namespace PageExtractor
{
    public partial class Form1 : Form
    {

        static string exePath = System.Environment.CurrentDirectory;//本程序所在路径

        //创建连接对象
        OleDbConnection conn = new OleDbConnection("provider=Microsoft.Jet.OLEDB.4.0;data source=" + exePath + @"\spider1.mdb");

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //链接数据库
            conn.Open();

            Console.WriteLine(exePath);
            searchAccess();
        }


        public void connectAccess()
        {

        }

        private void searchAccess()
        {

            //获取数据表
            //string sql = "select * from 表名 order by 字段1";
            //查询
            string sql = "select * from spiderStore where ID=1";

            OleDbDataAdapter da = new OleDbDataAdapter(sql, conn); //创建适配对象
            DataTable dt = new DataTable(); //新建表对象
            da.Fill(dt); //用适配对象填充表对象
            dataGridView1.DataSource = dt; //将表对象作为DataGridView的数据源


        }

        private void userSearchAccess()
        {
            if(textBox_urlSearch.Text=="")
            {
                MessageBox.Show("请输入url");
                return;
            }

            string sql = "select * from spiderStore where url like'%" + textBox_urlSearch.Text + "%'";
            OleDbDataAdapter da = new OleDbDataAdapter(sql, conn); //创建适配对象
            DataTable dt = new DataTable(); //新建表对象
            da.Fill(dt); //用适配对象填充表对象
            dataGridView1.DataSource = dt; //将表对象作为DataGridView的数据源
        }

        private void button1_Click(object sender, EventArgs e)
        {
            userSearchAccess();
        }

        //增删改
        private void editAccess()
        {

            //增
            string sql = "insert into 表名(字段1,字段2,字段3,字段4)values(...)";
            //删 
            //string sql = "delete from 表名 where 字段1="...; 
            //改 
            //string sql = "update student set 学号=" ...; 

            OleDbCommand comm = new OleDbCommand(sql, conn);
            comm.ExecuteNonQuery();


        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            conn.Close();
        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {

        }
    }
}
