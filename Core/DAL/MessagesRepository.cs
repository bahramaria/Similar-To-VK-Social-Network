﻿

using Core.DAL.Interfaces;
using Core.POCO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace Core.DAL
{
    public class MessagesRepository : IMessagesRepository
    {
        PhotoRepository photoRepository;
        private DBContext db;
        
        public MessagesRepository(DBContext db)
        {
            this.db = db;
            photoRepository = new PhotoRepository(db);
        }
        public int GetLastDialogID()
        {
            var message = db.Messages.OrderByDescending(a => a.DialogID).FirstOrDefault();

            if (message != null)
                return message.DialogID;

            return 0;
        }
        public string CreateNewDialog(Message message, string userId)//, string baseUrl = null)
        {
            string date = string.Format("{0}.{1}.{2}  {3}:{4}", DateTime.Today.Day, DateTime.Today.Month, DateTime.Today.Year, DateTime.Now.Hour, DateTime.Now.Minute);
            
            var messageFromDb = db.Messages.FirstOrDefault(a =>
                                        (a.SendersUserID == userId && a.ReceiversUserID == message.ReceiversUserID)
                                        ||
                                        (a.SendersUserID == message.ReceiversUserID && a.ReceiversUserID == userId)
                                );


            //Добавление сообщения в ранее созданный диалог
            if (messageFromDb != null)
            {
                var message_ = new Message
                {
                    Body = message.Body,
                    SendersUserID = userId,
                    DialogID = messageFromDb.DialogID,
                    RequestDate = DateTime.Now,
                    ViewedByReceiver = false,
                    ReceiversUserID = message.ReceiversUserID
                };

                InsertMessage(message_);

                return "Добавлено новое сообщения в ранее созданный диалог";
            }

            //Cоздание нового диалога
            else
            {
                message.Body = message.Body;
                message.SendersUserID = userId;
                message.DialogID = GetLastDialogID() + 1;
                message.RequestDate = DateTime.Now;
                message.ViewedByReceiver = false;

                InsertMessage(message);

                return "Cоздан новый диалог и отправлено первое сообщение";
            }

        }

        public void InsertMessage(Message message)
        {
            db.Entry(message).State = System.Data.Entity.EntityState.Added;
            db.SaveChanges();
        }

        public void CheckTheReceiverOpenedDialogToRead(string UserID, int dialogId)
        {
            var messages = db.Messages.Where(a => a.DialogID == dialogId);

            if (messages != null)
                foreach (var m in messages)
                {
                    if (m.ViewedByReceiver == false && m.ReceiversUserID == UserID)
                        m.ViewedByReceiver = true;
                }
        }


        public IQueryable<Message> GetUnviewedMessagesByReceiversUserID(string UserID)
        {
            var messages = db.Messages.Where(a => a.ReceiversUserID == UserID && !a.ViewedByReceiver);

            return messages;
        }

        

        private List<Message> ExcludeInvitationMessagesWhichWereSentByCurrentUser(List<Message> messages, string currentUserId)
        {
            var msgs = messages.Where(a => a.SendersUserID == currentUserId && a.Invitation == true).ToList();

            if (msgs != null)
                foreach (var msg in msgs)
                    messages.Remove(msg);

            return messages;
        }
        public List<string> GetDialogByID(int dialogID, string currentUserId)
        {
            var dialog = new List<string>();

            List<Message> messages = db.Messages.Where(a => a.DialogID == dialogID).ToList();

            if (messages != null)
            {
                messages = ExcludeInvitationMessagesWhichWereSentByCurrentUser(messages, currentUserId);

                foreach (var m in messages)
                {
                    dialog.Add(m.Body);
                }
            }

            return dialog;

        }

        public int GetDialogIdOfUsers(string fromUserId, string toUserId)
        {
            var dialog = db.Messages.FirstOrDefault(a => a.SendersUserID == fromUserId && a.ReceiversUserID == toUserId);

            if (dialog == null)
                dialog = db.Messages.FirstOrDefault(a => a.SendersUserID == toUserId && a.ReceiversUserID == fromUserId);

            if (dialog != null)
                return dialog.DialogID;

            return 0;
        }

        public List<Message> GetLastMessages(string userID)
        {
            List<Message> lastMessages = db.Messages
                            .Where(a => (a.ReceiversUserID == userID || a.SendersUserID == userID))

                            .GroupBy(key => key.DialogID,
                                            (k, g) => g.OrderByDescending(a => a.MessageID)
                            .FirstOrDefault())
                            .ToList();

            return lastMessages;
        }

        public List<Message> GetMessagesByDialogId(int dialogId, string currentUserId)
        {
            List<Message> messages = db.Messages.Where(a => a.DialogID == dialogId).ToList();
            List<Message> filteredMessages = this.ExcludeInvitationMessagesWhichWereSentByCurrentUser(messages, currentUserId);
            return filteredMessages;
        }


    }
}
