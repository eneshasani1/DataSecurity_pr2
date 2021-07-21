﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using JWT;
using JWT.Algorithms;
using JWT.Serializers;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Net.Sockets;
using System.Net;
using DataSecurity_pr2;
using System.Threading;
using DataSecurity_pr2.Repositories;
using DataSecurity_pr2.Models;

namespace Siguri_Projekti2
{
    class ServerSide
    {
       public static X509Certificate2 certifikata = new X509Certificate2("../../Siguri_Projekti2.cer", "123456");
        
        private const String secret = "enesh";
        private DESCryptoServiceProvider des;
        private RSACryptoServiceProvider rsa;
        static byte[] DesKey;
        private Socket server;

        public string Encrypt(string response) {
            byte[] byteResponse = Encoding.UTF8.GetBytes(response);
             des = new DESCryptoServiceProvider();
            //  des.Key = DesKey;
            des.GenerateKey();
            des.Mode = CipherMode.CBC;
            des.Padding = PaddingMode.Zeros;
            des.GenerateIV(); //gjenerimi i IV`
            byte[] IV = des.IV;
            MemoryStream ms = new MemoryStream();
            CryptoStream cs = new CryptoStream(ms,des.CreateEncryptor(),CryptoStreamMode.Write);
            cs.Write(byteResponse,0, byteResponse.Length);
            cs.Close();
            byte[] encryptedResponse = ms.ToArray();
            byte[] concatenatedResponse = IV.Concat(encryptedResponse).ToArray();
            return Convert.ToBase64String(concatenatedResponse);
            
        }
        public string Decrypt(string clientMessage) {
            string[] explodedMessages = clientMessage.Split(',');
            byte[] IV = Convert.FromBase64String(explodedMessages[0]);
            byte[] enDesKey = Convert.FromBase64String(explodedMessages[1]);
            byte[] enMessage = Convert.FromBase64String(explodedMessages[2]);
            byte[] desKey = rsa.Decrypt(enDesKey,false);
            DES des = DES.Create();
            des.IV = IV;
            des.Key = desKey;
            des.Mode = CipherMode.CBC;
            des.Padding = PaddingMode.Zeros;

            MemoryStream memoryStream = new MemoryStream(enMessage);
            byte[] decryptedMessage = new byte[memoryStream.Length];
            CryptoStream cryptoStream = new CryptoStream(memoryStream, des.CreateDecryptor(), CryptoStreamMode.Read);
            cryptoStream.Read(enMessage, 0, enMessage.Length);
            cryptoStream.Close();
            string decryptedData = Encoding.UTF8.GetString(decryptedMessage);
            return decryptedData;
            // login-...
        }

        public void createResponseToUser(Socket user) {
            while (true)
            {
                byte[] bytes = new Byte[1024];
                string data = null;
                int numBytes = user.Receive(bytes);
                data += Encoding.UTF8.GetString(bytes, 0, numBytes);

                string plainData = Decrypt(data);
                string logOrRegOrBill = plainData.Split('-')[0];
                string command = plainData.Split('-')[1];

                //komanda per login=emaili,pw
                //per register=emri,mbi,imella,id,pw
                //per fatura=id,viti,muji,...,...
//login-command
                switch (logOrRegOrBill)
                {
                    case "login":
                        string emaili = command.Split('>')[0];
                        if (UserRepository.findUser(emaili) == null)
                        {
                            string encryptedResponse = Encrypt("ERROR");
                            user.Send(Encoding.UTF8.GetBytes(encryptedResponse));
                        }
                        else
                        {
                            user.Send(Encoding.UTF8.GetBytes(Encrypt(JWTSignature(emaili))));
                        }
                        break;

                    case "register":
                        string userEmail = command.Split('>')[2];
                        if (UserRepository.findUser(userEmail) == null)
                        {
                            string name = command.Split('>')[0];
                            string surname = command.Split('>')[1];
                            string email = command.Split('>')[2];
                            int id = Convert.ToInt32(command.Split('>')[3]);
                            string password = command.Split('>')[4];
                            string salt = command.Split('>')[5];
                            User useri = new User(name, surname, email, id, password, salt);
                            if (UserRepository.createUser(useri))
                            {
                                user.Send(Encoding.UTF8.GetBytes(Encrypt("OK")));
                            }
                            else
                            {

                                user.Send(Encoding.UTF8.GetBytes(Encrypt("ERROR")));
                            }
                        }
                        else
                        {

                            user.Send(Encoding.UTF8.GetBytes(Encrypt("ERROR")));
                        }
                        break;

                    case "registerbill":
                        string type = command.Split('>')[0];
                        int year = Convert.ToInt32(command.Split('>')[1]);
                        string month = command.Split('>')[2];
                        double value = Convert.ToDouble(command.Split('>')[3]);
                        int userId = Convert.ToInt32(command.Split('>')[4]);
                        Bill bill = new Bill(type, year, month, value, userId);
                        BillRepository.addBill(bill);
                        user.Send(Encoding.UTF8.GetBytes(Encrypt("OK")));
                        break;
                    default:
                        break;
                }
            }
            //user.Shutdown(SocketShutdown.Both);
            //user.Close();
        }
      
        public static string JWTSignature(string email) {
            User useri = UserRepository.findUser(email);

            IJwtAlgorithm alg = new RS256Algorithm(certifikata);
            IJsonSerializer serializer = new JsonNetSerializer();
            IBase64UrlEncoder base64 = new JwtBase64UrlEncoder();
            //IJwtValidator jwt = new JwtValidator(serializer,new UtcDateTimeProvider());
            IJwtEncoder ije = new JwtEncoder(alg, serializer, base64);
            var payload = new Dictionary<string, object>
            {
                {"Userid",useri.getId()},
                {"Name",useri.getName()},
                {"Surname",useri.getSurname()},
                {"Email",useri.getEmail()},
                {"Password",useri.getPassword()},
                {"Salt",useri.getSalt()}
            };

            var token = ije.Encode(payload, secret);
            string signedMessage = token;

            return signedMessage;
        }
        public  ServerSide()
        {
            try
            {
                IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipAddr = ipHost.AddressList[0];
                IPEndPoint localEndPoint = new IPEndPoint(ipAddr, 11111);
                server = new Socket(ipAddr.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                server.Bind(localEndPoint);
                // rsa = (RSACryptoServiceProvider)certifikata.PrivateKey;
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        //public void listen()
        //{
        //    server.Listen(10);

        //    while (true)
        //    {

        //        Socket client = server.Accept();
        //        Thread thread = new Thread(() => this.sendResponseToUser(client));
        //        thread.Start();


        //    }
        //}

    }

}
