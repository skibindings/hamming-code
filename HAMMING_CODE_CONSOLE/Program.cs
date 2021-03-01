using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace HAMMING_CODE_CONSOLE
{
    class Program
    {
        static int hamming_word_size = 74;

        static void Main(string[] args)
        {
            byte[] inputBuffer = new byte[10240];
            Stream inputStream = Console.OpenStandardInput(inputBuffer.Length);
            Console.SetIn(new StreamReader(inputStream, Console.InputEncoding, false, inputBuffer.Length));

            bool reciever = false;
            while(true)
            {
                Console.WriteLine("Choose regime: S - sender, R - reciever:");
                string message = Console.ReadLine();
                if(message.Equals("S"))
                {
                    reciever = false;
                    Console.WriteLine("You are Sender!");
                    break;
                }
                else if(message.Equals("R"))
                {
                    reciever = true;
                    Console.WriteLine("You are Reciever!");
                    break;
                }
                else
                {
                    Console.WriteLine("Incorrect input!");
                }
            }

            if(!reciever)
            {
                SenderLogic();
            }
            else
            {
                RecieverLogic();
            }

            Console.ReadKey();
        }
        
        static void SenderLogic()
        {
            Console.WriteLine("Enter Server IP:");
            String ip = Console.ReadLine();
            Console.WriteLine("Enter Server port:");
            int port = int.Parse(Console.ReadLine());

            try
            {
                IPEndPoint ipPoint = new IPEndPoint(IPAddress.Parse(ip), port);
                
                do
                {
                    Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.Connect(ipPoint);

                    Console.WriteLine("Enter message to send:");
                    string message = Console.ReadLine();
                    Console.WriteLine();

                    Console.WriteLine("Enter regime:");
                    Console.WriteLine("0 - no errors");
                    Console.WriteLine("1 - one error per word max");
                    Console.WriteLine("2 - M errors per word max");
                    Console.WriteLine();

                    int regime = int.Parse(Console.ReadLine());
                    int M = 0;
                    if (regime == 1)
                    {
                        M = 1;
                    }
                    else if (regime == 2)
                    {
                        Console.Write("Enter M:");
                        M = int.Parse(Console.ReadLine());
                        Console.WriteLine();
                    }

                    int input_length;
                    byte[] encoded_data = EncodeHamming(message, out input_length);
                    encoded_data = GenerateMistakes(encoded_data, M);

                    Console.WriteLine("Generating mistakes... ");
                    Console.WriteLine();
                    Console.WriteLine("No mistakes words: " + no_mistakes);
                    Console.WriteLine("Single mistakes words: " + single_mistakes);
                    Console.WriteLine("Multiple mistakes words: " + multiple_mistakes);
                    Console.WriteLine();
                    Console.WriteLine("\nSending to server...");
                    Console.WriteLine();

                    byte[] to_send_data = new byte[encoded_data.Length + 8];
                    byte[] length_bytes = BitConverter.GetBytes(input_length);
                    byte[] size_bytes = BitConverter.GetBytes(encoded_data.Length);

                    for (int i = 0; i < 4; i++)
                    {
                        to_send_data[i] = length_bytes[i];
                        to_send_data[4 + i] = size_bytes[i];
                    }

                    for (int i = 0; i < encoded_data.Length; i++)
                    {
                        to_send_data[8 + i] = encoded_data[i];
                    }

                    socket.Send(to_send_data); // send extended hamming encoded text

                    // recieve answer
                    byte[] data = new byte[256]; // buffer for answer
                    StringBuilder builder = new StringBuilder();
                    int bytes = 0; // bytes amount

                    do
                    {
                        bytes = socket.Receive(data, data.Length, 0);
                        builder.Append(Encoding.UTF8.GetString(data, 0, bytes));
                    }
                    while (socket.Available > 0);
                    Console.WriteLine("\nServer answer:");
                    Console.WriteLine("Message Statistics: ");
                    Console.WriteLine(builder.ToString());

                    Console.WriteLine("Finish Work? (Y - yes)");

                    // close socket
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                }
                while (!Console.ReadLine().Equals("Y"));

                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static void RecieverLogic()
        {
            Console.WriteLine("Enter Server IP:");
            String ip = Console.ReadLine();
            Console.WriteLine("Enter Server port:");
            int port = int.Parse(Console.ReadLine());
            Console.WriteLine();

            try
            {
                IPEndPoint ipPoint = new IPEndPoint(IPAddress.Parse(ip), port);

                Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                listenSocket.Bind(ipPoint);

                // start listening
                listenSocket.Listen(10);

                Console.WriteLine("Server is launched... Wating for sender");
                Console.WriteLine();

                while (true)
                {
                    Socket handler = listenSocket.Accept();
                    // revieve 
                    StringBuilder builder = new StringBuilder();
                    int bytes = 0; 
                    byte[] data = new byte[10240]; 
                    do
                    {
                        bytes = handler.Receive(data);
                    }
                    while (handler.Available > 0);

                    byte[] length_bytes = new byte[4];
                    byte[] size_bytes = new byte[4];

                    for (int i = 0; i < 4; i++)
                    {
                        length_bytes[i] = data[i];
                        size_bytes[i] = data[4+i];
                    }

                    int length = BitConverter.ToInt32(length_bytes, 0);
                    int size = BitConverter.ToInt32(size_bytes, 0);

                    byte[] real_data = new byte[size];
                    for(int i = 0; i < size; i++)
                    {
                        real_data[i] = data[8 + i];
                    }


                    Console.WriteLine("Message recieved... Decoding");
                    Console.WriteLine();
                    String decoded_message = DecodeHamming(real_data, length);
                    Console.WriteLine("Decoded Message: ");
                    Console.WriteLine();
                    Console.WriteLine(decoded_message);
                    Console.WriteLine();


                    String stats = "correct_words: " + correct_words + "\n" +
                        "incorrect_words: " + incorrect_words + "\n" +
                        "mistakes_corrected: " + mistakes_corrected + "\n" +
                        "mistakes_not_corrected: " + mistakes_not_corrected + "\n";
                    Console.WriteLine(stats);

                    Console.WriteLine("Sending Message Statistics to client...");
                    

                    // send answer
                    data = Encoding.UTF8.GetBytes(stats);
                    handler.Send(data);
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static int single_mistakes = 0;
        static int multiple_mistakes = 0;
        static int no_mistakes = 0;

        static byte[] GenerateMistakes(byte[] input, int M)
        {
            single_mistakes = 0;
            multiple_mistakes = 0;
            no_mistakes = 0;

            Random random = new Random();

            BitArray input_bits = new BitArray(input);

            int total_word_size = hamming_word_size + 8;
            int words_amount = input_bits.Length / total_word_size;

            double random_window = random.NextDouble() * 0.4;

            for(int i = 0; i < words_amount; i++)
            {
                int _i = i * total_word_size;
                if (random.NextDouble() <= 0.3+ random_window && M > 0) 
                {
                    int mistakes_num = random.Next(1, M+1);
                    if (mistakes_num == 1) single_mistakes++;
                    else multiple_mistakes++;

                    List<int> mistake_indexes = new List<int>();
                    for(int j = 0; j < mistakes_num; j++)
                    {
                        int inverse_index;
                        do
                        {
                            inverse_index = random.Next(0, total_word_size);
                        }
                        while (mistake_indexes.Contains(inverse_index));

                        input_bits.Set(_i + inverse_index, !input_bits.Get(_i + inverse_index));
                        mistake_indexes.Add(inverse_index);
                    }
                }
                else
                {
                    no_mistakes++;
                }
            }


            return BitArrayToByteArray(input_bits);
        }

        static byte[] EncodeHamming(String text, out int bit_length)
        {
            BitArray input_bits = new BitArray(Encoding.UTF8.GetBytes(text));
            bit_length = input_bits.Length;

            //1, 2, 4, 8, 16, 32, 64, parity = 8 (extended hamming)
            int total_word_size = hamming_word_size + 8;
            int words_amount = (int)Math.Ceiling((double)input_bits.Length / (double)hamming_word_size);
            int enc_text_bit_size = words_amount * total_word_size;

            BitArray enc_bits = new BitArray(enc_text_bit_size);

            int address = 0;
            for (int i = 0; i < words_amount; i++)
            {
                int _i = i * total_word_size;

                bool full_parity = false;
                for (int j = 0; j < total_word_size-1; j++) // last bir for parity
                {
                    byte index = (byte)(j + 1);
                    if (!IsPowerOfTwo(index))
                    {
                        bool bit = false;
                        if(address < input_bits.Length)
                            bit = input_bits.Get(address);

                        enc_bits.Set(_i + j, bit);
                        full_parity ^= bit;

                        for (int p = 0; p < 7; p++)
                        {
                            int parity_index = (1 << p) - 1;
                            if (j >= parity_index)
                            {
                                if (IsBitSet(index, p))
                                {
                                    enc_bits.Set(_i + parity_index, enc_bits.Get(_i + parity_index) ^ bit);
                                }
                            }
                        }
                        address++;
                    }
                }

                // Finish calculating full parity
                for (int p = 0; p < 7; p++)
                {
                    int parity_index = (1 << p) - 1;
                    bool bit = enc_bits.Get(_i + parity_index);
                    full_parity ^= bit;
                }

                enc_bits.Set(_i + hamming_word_size + 7, full_parity);
            }
            return BitArrayToByteArray(enc_bits);
        }

        static int incorrect_words = 0;
        static int correct_words = 0;
        static int mistakes_corrected = 0;
        static int mistakes_not_corrected = 0;

        static String DecodeHamming(byte[] bytes, int output_length)
        {
            // reset all statistics
            incorrect_words = 0;
            correct_words = 0;
            mistakes_corrected = 0;
            mistakes_not_corrected = 0;

            BitArray input_bits = new BitArray(bytes);

            //1, 2, 4, 8, 16, 32, 64, parity = 8 (extended hamming)
            int total_word_size = hamming_word_size + 8;
            int words_amount = input_bits.Length / total_word_size; 
            int dec_text_bit_size = output_length;

            BitArray dec_bits = new BitArray(dec_text_bit_size);

            int address = 0;

            for (int i = 0; i < words_amount; i++)
            {
                int _i = i * total_word_size;

                bool full_parity = false;
                BitArray syndrome_bits = new BitArray(7);

                for (int j = 0; j < total_word_size-1; j++)
                {
                    byte index = (byte)(j + 1);

                    bool bit = input_bits.Get(_i + j);
                    full_parity ^= bit;

                    for (int p = 0; p < 7; p++)
                    {
                        int parity_index = (1 << p) - 1;
                        if (j >= parity_index)
                        {
                            if (IsBitSet(index, p))
                            {
                                syndrome_bits.Set(p, syndrome_bits.Get(p) ^ bit);
                            }
                        }
                    }
                }
                full_parity ^= input_bits.Get(_i + total_word_size - 1);

                int hamming_error_index = GetIntFromBitArray(syndrome_bits) - 1;
                if(hamming_error_index == -1)
                {
                    if(full_parity == true)
                    {
                        incorrect_words++;
                        mistakes_corrected++;
                    } 
                    else
                    {
                        correct_words++;
                    }
                }
                else
                {
                    incorrect_words++;
                    if (full_parity == false) // double error 
                    {
                        mistakes_not_corrected++;
                    }
                    else // one error can fix 
                    {
                        input_bits.Set(_i + hamming_error_index, !input_bits.Get(_i + hamming_error_index)); // invert bit
                        mistakes_corrected++;
                    }
                }

                // write to output
                for (int j = 0; j < total_word_size - 1; j++)
                {
                    byte index = (byte)(j + 1);

                    bool bit = input_bits.Get(_i + j);
                    if (!IsPowerOfTwo(index))
                    {
                        if(address < dec_text_bit_size)
                        {
                            dec_bits.Set(address, bit);
                        }
                        address++;
                    }
                }
            }
            return Encoding.UTF8.GetString(BitArrayToByteArray(dec_bits));
        }

        static byte[] BitArrayToByteArray(BitArray bits)
        {
            byte[] ret = new byte[(bits.Length - 1) / 8 + 1];
            bits.CopyTo(ret, 0);
            return ret;
        }

        static bool IsPowerOfTwo(int x)
        {
            return (x & (x - 1)) == 0;
        }

        static bool IsBitSet(byte b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }

        static int GetIntFromBitArray(BitArray bitArray)
        {

            if (bitArray.Length > 32)
                throw new ArgumentException("Argument length shall be at most 32 bits.");

            int[] array = new int[1];
            bitArray.CopyTo(array, 0);
            return array[0];

        }
    }
}
