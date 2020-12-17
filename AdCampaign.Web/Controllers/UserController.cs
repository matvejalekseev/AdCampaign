﻿using System.Threading.Tasks;
using AdCampaign.BLL.Services.Users;
using AdCampaign.DAL.Entities;
using AdCampaign.Models;
using Microsoft.AspNetCore.Mvc;

namespace AdCampaign.Controllers
{
    public class UserController :  Controller
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("Users")]
        public async Task<ViewResult> List()
        {
            var users = await _userService.GetAllUsers();
            return View(new UserListViewModel
            {
                Users = users
            });
        }
        
        [HttpGet]
        public async Task<ViewResult> Create()
        {
            return View();
        }
        
        [HttpPost]
        public async Task<IActionResult> Create(
            string username,
            string password,
            string email,
            string phone,
            Role role)
        {
            await _userService.CreateUser(username, password, email, phone, role);
            return RedirectToAction("List", "User");
        }
        
        [HttpGet("User/{id}/edit")]
        public async Task<IActionResult> Edit(long id)
        {
            var user = await _userService.Get(id);
            return View(new UserEditViewModel
            {
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                IsActive = !user.IsBlocked,
                UserId = user.Id
            });
        }
        
        [HttpPost("User/{id}/edit")]
        public async Task<IActionResult> Edit(
            long id,
            string username,
            string password,
            string email,
            string phone,
            Role role)
        {
            await _userService.UpdateUser(id, username, password, email, phone, role);
            return RedirectToAction("Edit", "User",new {id});
        }
        
        [HttpPost("User/block")]
        public async Task<IActionResult> Block(long id)
        {
            await _userService.BlockUser(id, null);
            return RedirectToAction("Edit", "User",new {id});
        }
        
        [HttpPost("User/delete")]
        public async Task<IActionResult> Delete(long id)
        {
            await _userService.Delete(id);
            return RedirectToAction("List", "User");
        }
    }
}