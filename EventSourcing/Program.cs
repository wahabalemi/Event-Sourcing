using System;
using System.Collections.Generic;
using System.Linq;


namespace EventSourcing
{
    #region Events

    public abstract class Event
    {
        public Guid Id { get; protected set; }
        public DateTime Timestamp { get; protected set; }

        public Event(Guid id)
        {
            Id = id;
            Timestamp = DateTime.UtcNow;
        }
    }

    public class AccountOpenedEvent : Event
    {
        public string AccountNumber { get; set; }
        public string AccountHolder { get; set; }

        public AccountOpenedEvent(Guid id, string accountNumber, string accountHolder)
            : base(id)
        {
            AccountNumber = accountNumber;
            AccountHolder = accountHolder;
        }
    }

    public class FundsWithdrawnEvent : Event
    {
        private decimal amount;

        private decimal balance;
        public decimal Balance { get { return balance; } }
        public FundsWithdrawnEvent(Guid id, decimal amount, decimal balance) : base(id)
        {
            Id = id;
            this.amount = amount;
            this.balance = balance;
        }
    }

    internal class FundsDepositedEvent : Event
    {
        private decimal amount;
        private decimal balance;
        public decimal Balance { get { return balance; } }
        public FundsDepositedEvent(Guid id, decimal amount, decimal balance) : base(id)
        {
            Id = id;
            this.amount = amount;
            this.balance = balance;
        }
    }

    #endregion

    public abstract class AggregateRoot
    {
        private readonly List<Event> _changes = new List<Event>();

        protected AggregateRoot(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; protected set; }

        public int Version { get; protected set; } = -1;

        public void LoadFromHistory(IEnumerable<Event> history)
        {
            foreach (var @event in history)
            {
                ApplyChange(@event, false);
            }
        }

        public IEnumerable<Event> GetUncommittedChanges()
        {
            return _changes;
        }

        public void MarkChangesAsCommitted()
        {
            _changes.Clear();
        }

        protected void ApplyChange(Event @event)
        {
            ApplyChange(@event, true);
        }

        private void ApplyChange(Event @event, bool isNew)
        {
            if (isNew)
            {
                _changes.Add(@event);
            }

            Apply(@event);
            Version++;
        }

        protected abstract void Apply(Event @event);
    }

    public class BankAccount : AggregateRoot
    {
        private string _accountNumber;
        private string _accountHolder;
        private decimal _balance;

        public decimal Balance { get { return _balance; } }

        public BankAccount(Guid id)
           : base(id)
        {
            Id = id;
        }

        public BankAccount(Guid id, string accountNumber, string accountHolder)
            : base(id)
        {
            Id = id;
            _accountNumber = accountNumber;
            _accountHolder = accountHolder;
            ApplyChange(new AccountOpenedEvent(id, accountNumber, accountHolder));
        }

        public void Deposit(decimal amount)
        {
            _balance += amount;
            ApplyChange(new FundsDepositedEvent(Id, amount, _balance));
        }

        public void Withdraw(decimal amount)
        {
            if (_balance >= amount)
            {
                _balance -= amount;
                ApplyChange(new FundsWithdrawnEvent(Id, amount, _balance));
            }
            else
            {
                throw new InvalidOperationException("Insufficient funds.");
            }
        }

        protected override void Apply(Event @event)
        {
            switch (@event)
            {
                case AccountOpenedEvent e:
                    _accountNumber = e.AccountNumber;
                    _accountHolder = e.AccountHolder;
                    break;
                case FundsDepositedEvent e:
                    _balance = e.Balance;
                    break;
                case FundsWithdrawnEvent e:
                    _balance = e.Balance;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}");
            }
        }
    }

    public class EventStore
    {
        private readonly List<Event> _events = new List<Event>();

        public void Save(Event @event)
        {
            _events.Add(@event);
        }

        public IEnumerable<Event> GetEvents()
        {
            return _events.AsEnumerable();
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            // Create a new event store
            var eventStore = new EventStore();

            // Create a new bank account
            var accountId = Guid.NewGuid();
            var account = new BankAccount(accountId, "1234567890", "John Doe");

            // Deposit some funds into the account
            account.Deposit(100.00m);

            account.Deposit(50.00m);

            account.Withdraw(25.00m);

            account.Deposit(1.00m);

            // Save the resulting events to the event store
            foreach (var @event in account.GetUncommittedChanges())
            {
                eventStore.Save(@event);
            }

            var events = eventStore.GetEvents();

            accountId = Guid.NewGuid();

            // Create a new bank account and apply the events to it
            var account1 = new BankAccount(accountId);

            account.LoadFromHistory(events);

            // The account balance should now be 100.00
            Console.WriteLine($"Account balance: {account.Balance}");

            Console.ReadLine();
        }
    }


   


}
