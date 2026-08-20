using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BankUIManager : MonoBehaviour
{
    [Header("Bank Related")]
    [Space(8)]
    public Bank bank;

    [Space(8)]
    public Text BudgetsText;    // Current Budget 
    public Text BalanceText;    // Current Balance 
    public Text TaxText;        // Current # of Tax Profits by Island 
    public Text IncomeText;     // Current # of Income
    public Text ExpenseText;    // Current # of Expenses
    public Text RevenueText;    // Current Profits Numbers
    public Text LicenseText;    // Current # of Licences

    private void Awake()
    {
        bank = FindObjectOfType<Bank>();
    }

    // Events

        // Subscribe to the bank's value changed event
        private void Start()
        {
            bank.OnBankValueChanged += UpdateBankUI;
        }

        // Subscribe to the bank's value changed event
        private void OnEnable()
        {
            bank.OnBankValueChanged += UpdateBankUI;
        }

        // Unsubscribe to prevent memory leaks
        private void OnDisable()
        {
            bank.OnBankValueChanged -= UpdateBankUI;
        }
        // Unsubscribe On Destruction
        private void OnDestroy()
        {
            // Unsubscribes on Destruction
            bank.OnBankValueChanged -= UpdateBankUI;
        }

    private void Update()
    {
        UpdateBankUI();
    }

    public void UpdateBankUI()
    {
        LicenseText.text = " £ " + bank.GetLicense();   // License
        BudgetsText.text = " € " + bank.GetBudget();    // Budget

        BalanceText.text = " € " + bank.GetBalance();   // Balance
        RevenueText.text = " € " + bank.GetRevenue();   // Revenue

        ExpenseText.text = " € " + bank.GetExpense();   // Expense
        IncomeText.text = " € " + bank.GetIncome();     // Income  
    }
}
