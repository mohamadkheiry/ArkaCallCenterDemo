using ArkaCallCenter.Core.Entities;

namespace ArkaCallCenter.Core.Abstractions;

public interface ITokenService
{
    string CreateToken(User user);
}
