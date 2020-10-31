﻿using EGameCafe.Application.Common.Interfaces;
using EGameCafe.Application.Common.Models;
using EGameCafe.Application.Models;
using EGameCafe.Application.Models.Identity;
using EGameCafe.Domain.Entities;
using EGameCafe.Infrastructure.PreparedResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace EGameCafe.Infrastructure.Identity
{
    public class IdentityService : IIdentityService
    {
      
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly IApplicationDbContext _applicationDbContext;
        private readonly IMobileSenders _mobileSenders;
        private readonly IEmailSender _emailSender;
        private readonly IMemoryCache _memoryCache;

        public IdentityService(UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            IApplicationDbContext applicationDbContext,
            RoleManager<IdentityRole> roleManager,
            IMobileSenders mobileSenders,
            IEmailSender emailSender,
            IMemoryCache memoryCache
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _applicationDbContext = applicationDbContext;
            _roleManager = roleManager;
            _mobileSenders = mobileSenders;
            _emailSender = emailSender;
            _memoryCache = memoryCache;
        }

        public async Task<Result> CreateUserAsync(RegisterModel model)
        {
            try
            {
                bool validEmail = IsValidEmail(model.Confirmation);
                
                var user = new ApplicationUser
                {
                    UserName = model.Username,
                    Email = model.Email,
                    FirstName = " ",
                    LastName = " ", 
                    BirthDate = model.BirthDate
                };

                if (!string.IsNullOrWhiteSpace(model.PhoneNumber)) user.PhoneNumber = model.PhoneNumber;

                var result = await _userManager.CreateAsync(user, model.Password);

                if (!result.Succeeded) return result.ToApplicationResult();

                //await _userManager.AddToRoleAsync(user, "User");

                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                
                return SendConfirmation(validEmail, token, user).Result;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<Result> RegisterUserInfo(RegisterUserInfoModel userInfo)
        {
            var user = await _userManager.FindByNameAsync(userInfo.Username);

            if (user == null)
            {
                return APIResults.UserNotFoundResult();
            }

            user.FirstName = userInfo.Firstname;
            user.LastName = userInfo.LastName;

            var result = await _userManager.UpdateAsync(user);

            return result.ToApplicationResult();
        }

        public async Task<string> GetUserNameAsync(string userId)
        {
            var user = await _userManager.Users.FirstAsync(u => u.Id == userId);

            return user.UserName;
        }

        private async Task<(Result result, string token)> OTPConfirmation(int OTPNumber, string email)
        {
            var otp = await _applicationDbContext.OTP
                .FirstOrDefaultAsync(e => e.RandomNumber == OTPNumber &&
                e.UserId == email && DateTime.Compare(DateTime.UtcNow, e.ExpiryDate) < 0 && e.Used == false);

            if (otp == null) return (APIResults.InvalidTokenResult("OTP is expired", "کد تایید نادرست است"), null);

            otp.Used = true;

            _applicationDbContext.OTP.Update(otp);

            await _applicationDbContext.SaveChangesAsync();

            return (Result.Success(), otp.Token);
        }

        public async Task<Result> AccountOTPConfirmation(OTPConfirmationModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                return APIResults.UserNotFoundResult();
            }

            var OTPResult = await OTPConfirmation(model.RandomNumber, model.Email);

            if (!OTPResult.result.Succeeded) return OTPResult.result;

            var result = await _userManager.ConfirmEmailAsync(user, OTPResult.token);

            return result.ToApplicationResult();
        }

        public async Task<Result> EmailConfirmation(EmailConfirmationModel model)
        {
            if (model.UserId == null || model.Token == null) return APIResults.BadRequestResult("userId or token is null", "شناسه کاربری یا توکن نادرست است");

            var user = await _userManager.FindByIdAsync(model.UserId);

            if (user == null) return APIResults.UserNotFoundResult();

            var result = await _userManager.ConfirmEmailAsync(user, model.Token);

            return result.ToApplicationResult();
        }

        public async Task<Result> ForgotPassword(SendOtpTokenModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null) return APIResults.UserNotFoundResult();

            if (!await _userManager.IsEmailConfirmedAsync(user)) return APIResults.NotConfirmedResult("Email not confirmed", "اکانت شما تایید نشده است");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            return await SendConfirmation(IsValidEmail(model.Confirmation), token, user);
        }

        public async Task<Result> ForgotPasswordOTPConfirmation(OTPConfirmationModel model)
        {
            var OTPResult = await OTPConfirmation(model.RandomNumber, model.Email);

            string cacheKey = model.Email + "ForgotPasswordOTPConfirmation";

            if (!OTPResult.result.Succeeded) return OTPResult.result;

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5));

            _memoryCache.Set(cacheKey, OTPResult.token, cacheEntryOptions);

            return Result.Success();
        }

        public async Task<Result> ResetPassword(ResetPasswordModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null) return APIResults.UserNotFoundResult();

            string cacheKey = model.Email + "ForgotPasswordOTPConfirmation";

            if (_memoryCache.TryGetValue(cacheKey, out string token))
            {
                var result = await _userManager.ResetPasswordAsync(user, token, model.Password);
                return result.ToApplicationResult();
            }

            return APIResults.InvalidTokenResult("Token is expired", "توکن شما انقضا شده است");
        }

        public async Task<Result> SendAccountConfirmationTokenAgain(SendOtpTokenModel model)
        {
            var user = await _userManager.FindByNameAsync(model.Email);

            if (user == null)
            {
                return APIResults.UserNotFoundResult();
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            return SendConfirmation(IsValidEmail(model.Confirmation), token, user).Result;
        }

        private async Task<Result> SendConfirmation(bool validEmail, string token, ApplicationUser user)
        {
            if (validEmail)
            {
                return await _emailSender.SendEmailAsync(user.Email, " ", "Confirmation");
            }
            else
            {
                return await _mobileSenders.SendOTP(user.PhoneNumber, user.Email, token);
            }
        }

        public async Task<Result> DeleteUserAsync(string userId)
        {
            var user = _userManager.Users.SingleOrDefault(u => u.Id == userId);

            if (user != null)
            {
                return await DeleteUserAsync(user);
            }

            return Result.Success();
        }

        public async Task<Result> DeleteUserAsync(ApplicationUser user)
        {
            var result = await _userManager.DeleteAsync(user);

            return result.ToApplicationResult();
        }

        public async Task<AuthenticationResult> Login(LoginModel model)
        {
            var user = new ApplicationUser();

            if (IsValidEmail(model.Username))
            {
                user = await _userManager.FindByEmailAsync(model.Username);
            }

            else user = await _userManager.FindByNameAsync(model.Username);


            if (user == null)
            {
                return new AuthenticationResult { Succeeded = false, EnError = "User not found", FaError = "حساب کاربری با این نام وجود ندارد" };
            }

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, false, true);

            if (user != null && result.Succeeded)
            {
                var auth = await GenerateToken(user);

                return auth;
            }

            if (result.IsNotAllowed)
            {
                return new AuthenticationResult { Succeeded = false, EnError = "account not confirmed", FaError = "اکانت شما فعال نشده است" };
            }

            return new AuthenticationResult { Succeeded = false, EnError = "username or password is wrong", FaError = "نام کاربری یا رمز عبور نادرست است" };

        }

        public async Task<AuthenticationResult> RefreshTokenAsync(RefreshTokenModel refreshToken)
        {
            var validateToken = GetPrincipalFromToken(refreshToken.Token);

            if (validateToken == null)
            {
                return new AuthenticationResult { Succeeded = false, EnError = "Invalid token", FaError = "توکن نامعتبر است" };
            }

            var expiryDateUnix = long.Parse(validateToken.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Exp).Value);

            var expiryDateTimeUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddSeconds(expiryDateUnix);

            if (expiryDateTimeUtc > DateTime.UtcNow)
            {
                return new AuthenticationResult { Succeeded = false, EnError = "This token hasn't expired yet", FaError = "توکن انقضا نشده است" };
            }

            var jti = validateToken.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Jti).Value;

            var storeRefreshToken = await _applicationDbContext.RefreshTokens.SingleOrDefaultAsync(x => x.Token == refreshToken.RefreshToken);

            if (storeRefreshToken == null)
            {
                return new AuthenticationResult { Succeeded = false, EnError = "This refresh token does not exist", FaError = "توکن بازیابی وجود ندارد" };
            }

            if (storeRefreshToken.Used)
            {
                return new AuthenticationResult { Succeeded = false, EnError = "Token is used ", FaError = "توکن استفاده شده است" };
            }

            if (DateTime.UtcNow > storeRefreshToken.ExpiryDate)
            {
                return new AuthenticationResult { Succeeded = false, EnError = "This refresh token has expired", FaError = "توکن بازیابی انقضا شده است" };
            }

            if (storeRefreshToken.InValidation)
            {
                return new AuthenticationResult { Succeeded = false, EnError = "This refresh token has been invalidated", FaError = "توکن بازیابی نا معتبر است" };
            }

            if (storeRefreshToken.JwtId != jti)
            {
                return new AuthenticationResult { Succeeded = false, EnError = "This refresh token does not match this JWT", FaError = "توکن بازیابی نادرست است" };
            }

            storeRefreshToken.Used = true;

            _applicationDbContext.RefreshTokens.Update(storeRefreshToken);

            await _applicationDbContext.SaveChangesAsync();

            var user = await _userManager.FindByIdAsync(validateToken.Claims.Single(x => x.Type == "id").Value);

            return await GenerateToken(user, storeRefreshToken.ExpiryDate);
        }

        public async Task<AuthenticationResult> GenerateToken(ApplicationUser user, DateTime date = default)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:SigningKey"]);
            var audience = _configuration["Jwt:Site"];
            int expiryInMinutes = Convert.ToInt32(_configuration["Jwt:ExpiryInMinutes"]);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                //new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("id", user.Id),
                new Claim(JwtRegisteredClaimNames.FamilyName, user.FullName())
            };

            var userClaims = await _userManager.GetClaimsAsync(user);
            claims.AddRange(userClaims);

            var userRoles = await _userManager.GetRolesAsync(user);

            foreach (var userRole in userRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, userRole));

                var role = await _roleManager.FindByNameAsync(userRole);

                var roleClaims = await _roleManager.GetClaimsAsync(role);

                foreach (var roleClaim in roleClaims)
                {
                    claims.Add(roleClaim);
                }
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = audience,
                Audience = audience,

                Subject = new ClaimsIdentity(claims),

                Expires = DateTime.UtcNow.AddMinutes(expiryInMinutes),
                //Expires = DateTime.UtcNow.AddMinutes(3),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);

            var refreshToken = new RefreshToken()
            {
                JwtId = token.Id,
                UserId = user.Id,
                CreationDate = DateTime.UtcNow,
                ExpiryDate = date.Year == 1 ? DateTime.UtcNow.AddMonths(6) : date
            };

            await _applicationDbContext.RefreshTokens.AddAsync(refreshToken);
            await _applicationDbContext.SaveChangesAsync();

            //refreshToken.Token is auto generated
            return new AuthenticationResult
            {
                Succeeded = true,
                RefreshToken = refreshToken.Token,
                Token = tokenHandler.WriteToken(token),
                Username = user.UserName
            };
        }

        private ClaimsPrincipal GetPrincipalFromToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:SigningKey"]);

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidAudience = _configuration["Jwt:Site"],
                ValidIssuer = _configuration["Jwt:Site"],
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false
            };

            try
            {
                var principle = tokenHandler.ValidateToken(token, tokenValidationParameters, out var validateToken);

                if (!IsJwtWithValidSecurityAlgorithm(validateToken))
                {
                    return null;
                }

                return principle;
            }
            catch (Exception)
            {
                return null;
            }

        }

        private bool IsJwtWithValidSecurityAlgorithm(SecurityToken validToken)
        {
            return (validToken is JwtSecurityToken jwtSecurityToken) &&
                jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase);
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
