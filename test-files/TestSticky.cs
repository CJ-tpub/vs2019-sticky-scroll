using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StickyScrollTest
{
    // 这是一个用于测试粘滞滚动的 C# 文件
    public class OuterClass
    {
        private readonly List<string> _items = new List<string>();

        public class InnerClass
        {
            public void DoWork(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    Console.WriteLine("item " + i);
                }
            }
        }

        public void MethodA()
        {
            // 方法 A 的注释
            var x = new InnerClass();
            x.DoWork(5);
        }

        public void MethodB()
        {
            if (DateTime.Now.DayOfWeek == DayOfWeek.Friday)
            {
                Console.WriteLine("It's Friday!");
            }
            else
            {
                Console.WriteLine("Not Friday.");
            }
        }

        public void MethodC()
        {
            try
            {
                var s = "a { not a brace } and /* not a comment */";
                Console.WriteLine(s);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public void MethodD()
        {
            var dict = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };
            foreach (var kv in dict)
            {
                Console.WriteLine(kv.Key + "=" + kv.Value);
            }
        }

        public void MethodE()
        {
            var lambda = new Func<int, int>(n =>
            {
                return n * 2;
            });
            Console.WriteLine(lambda(21));
        }

        public void MethodF()
        {
            while (true)
            {
                break;
            }
        }
    }
}
