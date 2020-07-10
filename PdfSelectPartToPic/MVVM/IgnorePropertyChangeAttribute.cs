using System;

namespace PdfSelectPartToPic.MVVM
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class IgnorePropertyChangeAttribute : Attribute
    {
         
        public IgnorePropertyChangeAttribute( )
        {
             
        }

    }
}