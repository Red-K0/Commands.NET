﻿using Commands.Core;

namespace Commands.Tests
{
    public sealed class Module : ModuleBase<ConsumerBase>
    {
        [Command("base-test")]
        public void Test()
        {

        }

        [Command("param-test")]
        public void Test(int i)
        {

        }

        [Group("nested")]
        public sealed class NestedModule : ModuleBase<ConsumerBase>
        {
            [Command("test")]
            public void Test()
            {

            }
        }
    }
}
