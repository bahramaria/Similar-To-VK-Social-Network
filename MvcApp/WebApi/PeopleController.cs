﻿

using Microsoft.AspNet.Identity;
using Core.BLL.DTO;
using Core.BLL.Interfaces;
using Core.POCO;
using MvcApp.ViewModel;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Web.Http;
using System.Web;
using System;

namespace MvcApp.WebAPI
{
    public class PeopleController : ApiController
    {
        ILuceneService luceneService;
        IMappingService mappingService;
        IUserService userService;
        IRelationshipsService relationshipsService;
        ICountriesService countriesService;
        IProfileService profileService;
        IPhotoService photoService;
        IChatHub chathub;

        string currentUserId;

        public PeopleController(IChatHub _chathub, IRelationshipsService _relationshipsService, ICountriesService _countriesService, IProfileService _profileService, IPhotoService _photoService, IUserService _userService, IMappingService _mappingService, ILuceneService _luceneService)
        {
            relationshipsService = _relationshipsService;
            countriesService = _countriesService;
            profileService = _profileService;
            photoService = _photoService;
            userService = _userService;
            mappingService = _mappingService;
            chathub = _chathub;
            luceneService = _luceneService;
            currentUserId = User.Identity.GetUserId();
        }

        [HttpDelete]
        [ActionName("RemoveFromFriends")]
        public HttpResponseMessage RemoveFromFriends(string id)
        {
            Relationship relationship = relationshipsService.GetRelationship(currentUserId, id);

            if (relationship == null)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }
            else
            {
                relationshipsService.RemoveFromFriends(relationship, currentUserId, id);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        }

        [HttpGet]
        [ActionName("GetCountries")]
        public HttpResponseMessage GetCountries()
        {
            HttpResponseMessage response = Request.CreateResponse();

            List<Country> countries = countriesService.GetAllCountries();
            string[] _countries = countries.Select(a => a.CountryName).ToArray();
            if (countries != null)
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new ObjectContent<string[]>(_countries, new JsonMediaTypeFormatter());
            }
            else
            {
                response.StatusCode = HttpStatusCode.NotFound;
                throw new HttpResponseException(response);
            }

            return response;
        }

        [HttpGet]
        [ActionName("GetTownsByCountry")]
        public HttpResponseMessage GetTownsByCountry(int id)
        {
            Town[] towns = countriesService.GetTownsByCountry(id);

            HttpResponseMessage response = null;

            if (towns != null)
            {
                response = Request.CreateResponse(HttpStatusCode.OK);
                response.Content = new ObjectContent<Town[]>(towns, new JsonMediaTypeFormatter());
            }

            else
                response = Request.CreateResponse(HttpStatusCode.NotFound, "NotFound");

            return response;
        }

        [HttpGet]
        [ActionName("GetTownsByCountryName")]
        public HttpResponseMessage GetTownsByCountryName(string id)
        {
            Town[] towns = countriesService.GetTownsByCountryName(id);

            string[] townNames = null;

            if (towns != null && towns.Length > 0)
                townNames = towns.Select(a => a.TownName).ToArray();

            HttpResponseMessage response = null;

            if (townNames != null)
                response = Request.CreateResponse(HttpStatusCode.OK, townNames);

            else
                response = Request.CreateResponse(HttpStatusCode.NotFound, "NotFound");

            return response;
        }


        [HttpGet]
        [ActionName("GetUsersList")]
        public HttpResponseMessage GetUsersList(int from, int to, int countryId, int? townId, string UserID, string gender, bool online = false)
        {
            List<UserViewModel> users = GetUsersList(from, to, countryId, townId, UserID, gender, null, online);

            HttpResponseMessage response = null;

            if (users != null)
            {
                response = Request.CreateResponse();
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new ObjectContent<List<UserViewModel>>(users, new JsonMediaTypeFormatter());
            }
            else
            {
                response = Request.CreateResponse(HttpStatusCode.NotFound, "NotFound");
            }
            return response;
        }



        [HttpGet]
        [ActionName("GetUsersList")]
        public List<UserViewModel> GetUsersList(int from, int to, int countryId, int? townId, string UserID, string gender, string name, bool online = false)
        {
            List<UserViewModel> users = null;
            List<Profile> filteredProfiles = null;

            //Friends list
            if (UserID != "0")
            {
                List<string> userIds = null;

                if (online == true)
                {
                    List<UserInfo> onlineFriends = chathub.GetOnlineFriends(UserID);
                    userIds = onlineFriends.Select(a => a.UserID).ToList();
                }
                else
                {
                    userIds = relationshipsService.GetFriendsIDsOfUser(UserID);
                }

                filteredProfiles = profileService.GetFriendsByUserID(from, to, countryId, townId, UserID, gender, userIds);
            }


            //Common people list
            else
            {
                IQueryable<Profile> profiles = profileService.GetAllProfiles();
                filteredProfiles = profileService.GetUsers(from, to, countryId, townId, gender, profiles);
            }

            //Mapping
            if (filteredProfiles.Count > 0)
                users = filteredProfiles.Select(a => new UserViewModel
                {
                    UserID = a.UserID,
                    ImageUrl = photoService.GetAvatar(a.UserID),
                    UserName = a.ApplicationUser.UserName,
                    FirstName = a.FirstName,
                    LastName = a.LastName
                })
                .ToList();

            //Lucene search
            if (users != null && users.Count > 0)
            {
                users = ExcludeFromResultCurrentUser(users, currentUserId);

                if (!string.IsNullOrEmpty(name) && name != "null")//js "null"
                {
                    List<UserDTO> usersDto = users.Select(a => mappingService.Map<UserViewModel, UserDTO>(a)).ToList();

                    List<UserDTO> _users = luceneService.UsersFromLuceneIndex(usersDto, name);

                    users = _users.Select(a => mappingService.Map<UserDTO, UserViewModel>(a)).ToList();
                }
            }

            return users;
        }

        private List<UserViewModel> ExcludeFromResultCurrentUser(List<UserViewModel> users, string userId)
        {
            var currentUser = users.FirstOrDefault(a => a.UserID == userId);

            if (currentUser != null)
                users.Remove(currentUser);

            return users;
        }




    }
}

