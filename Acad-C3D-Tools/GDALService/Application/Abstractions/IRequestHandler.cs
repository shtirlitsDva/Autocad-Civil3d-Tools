using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Application.Abstractions
{
    internal interface IRequestHandler<in TReq, TRes>
    {
        Task<TRes> HandleAsync(TReq request, CancellationToken ct = default);
    }
}
