using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
 
namespace TopLevel
{
   class C
{
    int N(int y){
        if (y%2==0){
            return y/2;
        }
        return y*2;
    }
    int M(int x)
    {
      x = 0;
      int y = x * 3;
      for (int i=0; i<10; i++){
        y+=x;
        if (y>100){
            x=-1;
        }
        else if (y<10){
            x=N(x);
        }
        else{
            break;
        }
      }
      return y;
    }
}
}