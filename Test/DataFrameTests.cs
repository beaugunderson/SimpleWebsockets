using System.Linq;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SimpleWebsockets.Server.Classes;

namespace WebSocketTest
{
    [TestClass]
    public class DataFrameTests
    {
        [TestMethod]
        public void TestDataFrameReflexivity()
        {   
            const string message = "Hello Server!";

            var dataFrame1 = new DataFrame(message);
            var dataFrame2 = new DataFrame(Encoding.UTF8.GetBytes(message));

            var dataFrame3 = DataFrame.FromRawBytes(dataFrame1.ToBytes());
            var dataFrame4 = DataFrame.FromRawBytes(dataFrame2.ToBytes());

            Assert.AreEqual(dataFrame1.Opcode, dataFrame3.Opcode);
            Assert.AreEqual(dataFrame2.Opcode, dataFrame4.Opcode);

            Assert.AreEqual(message, dataFrame1.Payload);
            Assert.AreEqual(message, dataFrame2.Payload);
            Assert.AreEqual(message, dataFrame3.Payload);
            Assert.AreEqual(message, dataFrame4.Payload);

            Assert.AreEqual(dataFrame1.Payload, dataFrame3.Payload);
            Assert.AreEqual(dataFrame2.Payload, dataFrame4.Payload);

            Assert.AreEqual(dataFrame1.PayloadLength, dataFrame3.PayloadLength);
            Assert.AreEqual(dataFrame2.PayloadLength, dataFrame4.PayloadLength);

            Assert.IsTrue(dataFrame1.PayloadBytes.SequenceEqual(dataFrame3.PayloadBytes));
            Assert.IsTrue(dataFrame2.PayloadBytes.SequenceEqual(dataFrame4.PayloadBytes));
            
            Assert.AreEqual(dataFrame1.IsMasked, dataFrame3.IsMasked);
            Assert.AreEqual(dataFrame2.IsMasked, dataFrame4.IsMasked);

            Assert.AreEqual(dataFrame1.IsFinished, dataFrame3.IsFinished);
            Assert.AreEqual(dataFrame2.IsFinished, dataFrame4.IsFinished);
        }

        [TestMethod]
        public void TestDataFrameFromStringAndBytes()
        {
            const string message = "Hello Server!";

            var messageBytes = Encoding.UTF8.GetBytes(message);

            var dataFrame1 = new DataFrame(message);
            var dataFrame2 = new DataFrame(Encoding.UTF8.GetBytes(message));

            Assert.AreEqual(message, dataFrame1.Payload);
            Assert.AreEqual(message, dataFrame2.Payload);

            Assert.IsTrue(messageBytes.SequenceEqual(dataFrame1.PayloadBytes));
            Assert.IsTrue(messageBytes.SequenceEqual(dataFrame2.PayloadBytes));
            
            Assert.AreEqual(messageBytes.Length, dataFrame1.PayloadLength);
            Assert.AreEqual(messageBytes.Length, dataFrame2.PayloadLength);

            Assert.IsTrue(dataFrame1.IsFinished);
            Assert.IsTrue(dataFrame2.IsFinished);

            Assert.IsFalse(dataFrame1.IsMasked);
            Assert.IsFalse(dataFrame2.IsMasked);

            Assert.AreEqual(2, dataFrame1.OffsetBytes);
            Assert.AreEqual(2, dataFrame2.OffsetBytes);

            Assert.AreEqual(1, dataFrame1.Opcode);
            Assert.AreEqual(1, dataFrame2.Opcode);

            Assert.IsFalse(dataFrame1.Reserved1);
            Assert.IsFalse(dataFrame1.Reserved2);
            Assert.IsFalse(dataFrame1.Reserved3);

            Assert.IsFalse(dataFrame2.Reserved1);
            Assert.IsFalse(dataFrame2.Reserved2);
            Assert.IsFalse(dataFrame2.Reserved3);
        }
    }
}