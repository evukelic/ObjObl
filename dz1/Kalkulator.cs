using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace PrvaDomacaZadaca_Kalkulator
{
    public class Factory
    {
        public static ICalculator CreateCalculator()
        {
            // vratiti kalkulator
            return new Kalkulator();
        }
    }

    public class Kalkulator:ICalculator
    {
        private string _firstOperand;
        private string _secondOperand;
        private string _currentOperation;

        private string _memory;
        private string _currentDisplayState;

        private static readonly char[] _binaryOperations = { '+', '-', '*', '/' };
        private static readonly char[] _unaryOperations = { 'M', 'S', 'K', 'T', 'Q', 'R', 'I' };
        private static readonly char[] _memoryOperations = { 'P', 'G' };

        private const char EQUALS = '=';
        private const char CLEAR = 'C';
        private const char ON_OFF = 'O';
        private const char DECIMAL = ',';

        private const string ZERO = "0";
        private const string ERROR = "-E-";
        private const string EMPTY = "";
        private const string MINUS = "-";

        private const int MAX_DIGITS = 10;
        private const int MAX_SIGNED_LENGTH = 12;
        private const int MAX_UNSIGNED_LENGTH = 11;
        private const int ROUNDING_DIGIT = 5;

        private const string CULTURE = "hr-HR";

        public Kalkulator()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(CULTURE);

            _currentDisplayState = ZERO;
        }

        /// <summary>
        /// Represents one calculator entry.
        /// </summary>
        /// <param name="inPressedDigit"> New calculator entry. </param>
        public void Press(char inPressedDigit)
        {
            if (!IsDigitValid(inPressedDigit))
            {
                ShowError();
                return;
            }

            ParseNewEntry(inPressedDigit);
        }

        /// <summary>
        /// Gets current display state of the calculator.
        /// </summary>
        /// <returns> Current display state. </returns>
        public string GetCurrentDisplayState()
        {
            RemoveInitialZero();
            _currentDisplayState = ClearPossibleZeroAfterDecimal(_currentDisplayState);
            // handle possible negative zero
            _currentDisplayState = Regex.Replace(_currentDisplayState, "^-0$", "0");
            return _currentDisplayState;
        }

        /// <summary>
        /// Parses new calculator entry and executes related mathematical operation.
        /// </summary>
        /// <param name="inPressedDigit"> New calculator entry. </param>
        private void ParseNewEntry(char inPressedDigit)
        {

            RemoveInitialZero();

            CharacterType characterType = GetCharacterType(inPressedDigit);

            switch (characterType)
            {
                case CharacterType.Number:
                    SetOperandAndDisplay(inPressedDigit);
                    break;

                case CharacterType.UnaryOperation:
                    ExecuteUnaryOperation(inPressedDigit.ToString());
                    break;

                case CharacterType.BinaryOperation:
                    if (string.IsNullOrEmpty(_firstOperand))
                    {
                        _firstOperand = ZERO;
                    }

                    bool shouldExecuteBinaryOperation = !string.IsNullOrEmpty(_firstOperand) && !string.IsNullOrEmpty(_secondOperand) && !string.IsNullOrEmpty(_currentOperation);
                    
                    if (shouldExecuteBinaryOperation)
                    {
                        _firstOperand = ExecuteBinaryOperation();
                    }
                    _currentOperation = inPressedDigit.ToString();
                    break;

                case CharacterType.MemoryOperation:
                    ExecuteMemoryOperation(inPressedDigit.ToString());
                    break;

                case CharacterType.Equals:
                    if (string.IsNullOrEmpty(_currentOperation))
                    {
                        _currentDisplayState = string.IsNullOrEmpty(_firstOperand) ? ZERO : ClearPossibleZeroAfterDecimal(_firstOperand);
                        return;
                    }
                    ExecuteBinaryOperation();
                    break;

                case CharacterType.OnOff:
                    ResetCalculator();
                    break;

                case CharacterType.Clear:
                    ClearRegister();
                    break;
            }
        }

        /// <summary>
        /// Removes initial zero from the display, if it's not part of a decimal number.
        /// </summary>
        private void RemoveInitialZero()
        {
            _currentDisplayState = Regex.Replace(_currentDisplayState, "^0+", ZERO);
            bool shouldZeroBeRemoved = _currentDisplayState.StartsWith(ZERO) && !_currentDisplayState.Contains(DECIMAL) && _currentDisplayState.Length > 1;

            if (shouldZeroBeRemoved)
            {
                _currentDisplayState = _currentDisplayState.Substring(1);
            }
        }

        /// <summary>
        /// Sets number to an operand and displays new entry.
        /// </summary>
        /// <param name="inPressedDigit"> New calculator entry. </param>
        private void SetOperandAndDisplay(char inPressedDigit)
        {
            bool shouldUseFirstOperand = string.IsNullOrEmpty(_firstOperand) || string.IsNullOrEmpty(_currentOperation);

            if (shouldUseFirstOperand)
            {
                _firstOperand += inPressedDigit.ToString();
            }
            else
            {
                _secondOperand += inPressedDigit.ToString();
            }

            _currentDisplayState += inPressedDigit.ToString();
        }

        /// <summary>
        /// Executes wanted unary operation and displays the result.
        /// </summary>
        /// <param name="operation"> Wanted unary operation. </param>
        private void ExecuteUnaryOperation(string operation)
        {
            double operand = ChooseUnaryOperand();
            double calculatedValue;

            switch (operation)
            {
                case "M":
                    ConvertSign();
                    return;
                case "S":
                    calculatedValue = Math.Sin(operand);
                    break;
                case "K":
                    calculatedValue = Math.Cos(operand);
                    break;
                case "T":
                    calculatedValue = Math.Tan(operand);
                    break;
                case "Q":
                    calculatedValue = Math.Pow(operand, 2);
                    break;
                case "R":
                    calculatedValue = Math.Sqrt(operand);
                    if (double.IsNaN(calculatedValue))
                    {
                        ShowError();
                        return;
                    }
                    break;
                case "I":
                    calculatedValue = 1 / operand;
                    if (double.IsInfinity(calculatedValue))
                    {
                        ShowError();
                        return;
                    }
                    break;
                default:
                    return;
            }

            string calculatedValueAsString = calculatedValue.ToString();

            bool isResultInvalid = IsResultTooBig(calculatedValueAsString);
            if (isResultInvalid)
            {
                ShowError();
                return;
            }

            SetAndDisplayUnaryResult(calculatedValueAsString);
        }

        /// <summary>
        /// Executes wanted binary operation and displays the result.
        /// </summary>
        /// <returns> Result of the binary operation. </returns>
        private string ExecuteBinaryOperation()
        {
            double firstOperand = string.IsNullOrEmpty(_firstOperand) ? double.Parse(ZERO) : double.Parse(_firstOperand);
            double secondOperand = GetSecondBinaryOperand(firstOperand);

            double calculatedValue;

            switch (_currentOperation)
            {
                case "+":
                    calculatedValue = firstOperand + secondOperand;
                    break;
                case "-":
                    calculatedValue = firstOperand - secondOperand;
                    break;
                case "*":
                    calculatedValue = firstOperand * secondOperand;
                    break;
                case "/":
                    calculatedValue = firstOperand / secondOperand;
                    if (double.IsInfinity(calculatedValue))
                    {
                        ShowError();
                        return EMPTY;
                    }
                    break;
                default:
                    return EMPTY;
            }

            string calculatedValueAsString = calculatedValue.ToString();

            bool isResultInvalid = IsResultTooBig(calculatedValueAsString);
            if (isResultInvalid)
            {
                ShowError();
                return EMPTY;
            }

            return DisplayAndGetBinaryResult(calculatedValueAsString);
        }

        /// <summary>
        /// Executes wanted memory operation.
        /// </summary>
        /// <param name="operation"> Wanted memory operation. </param>
        public void ExecuteMemoryOperation(string operation)
        {
            bool shouldUseFirstOperand = string.IsNullOrEmpty(_secondOperand);
            switch (operation)
            {
                case "P":
                    if (shouldUseFirstOperand)
                    {
                        _memory = string.IsNullOrEmpty(_firstOperand) ? ZERO : _firstOperand;
                    } else
                    {
                        _memory = _secondOperand;
                    }
                    
                    break;
                case "G":
                    if (string.IsNullOrEmpty(_memory))
                    {
                        _currentDisplayState = ZERO;
                        return;
                    }

                    _currentDisplayState = _memory;
                    if (shouldUseFirstOperand)
                    {
                        _firstOperand = _memory;
                    } else
                    {
                        _secondOperand = _memory;
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Resets all calculators registers and sets display to init value.
        /// </summary>
        private void ResetCalculator()
        {
            _currentOperation = EMPTY;
            _firstOperand = EMPTY;
            _secondOperand = EMPTY;
            _currentDisplayState = ZERO;
        }

        /// <summary>
        /// Clears last used calculators register.
        /// </summary>
        private void ClearRegister()
        {
            if (!string.IsNullOrEmpty(_secondOperand))
            {
                _secondOperand = EMPTY;
            }
            else if (!string.IsNullOrEmpty(_currentOperation))
            {
                _currentOperation = EMPTY;
            }
            else
            {
                _firstOperand = EMPTY;
            }

            _currentDisplayState = ZERO;
        }

        /// <summary>
        /// Chooses operand register for the unary operation based on a state of operand and operation registers.
        /// </summary>
        /// <returns> Operand on which operation will be executed. </returns>
        private double ChooseUnaryOperand()
        {
            bool shouldExecuteOnSecondOperand = !string.IsNullOrEmpty(_currentOperation) && !string.IsNullOrEmpty(_secondOperand);
            if (shouldExecuteOnSecondOperand)
            {
                return double.Parse(_secondOperand);
            }
            else
            {
                return string.IsNullOrEmpty(_firstOperand) ? double.Parse(ZERO) : double.Parse(_firstOperand);
            }
        }

        /// <summary>
        /// Converts number sign from signed to unsigned, and vice versa.
        /// </summary>
        private void ConvertSign()
        {
            bool shouldUseFirstOperand = string.IsNullOrEmpty(_currentOperation) && string.IsNullOrEmpty(_secondOperand);
            string result;

            if (shouldUseFirstOperand)
            {
                string firstOperandValue = string.IsNullOrEmpty(_firstOperand) ? ZERO : _firstOperand;
                result = SwitchSign(firstOperandValue);
                _firstOperand = result;
            }
            else
            {
                result = SwitchSign(_secondOperand);
                _secondOperand = result;
            }

            if (result.Equals(ERROR))
            {
                ShowError();
                return;
            }

            _currentDisplayState = result;
        }

        /// <summary>
        /// Checks if result is too big for calculator display.
        /// </summary>
        /// <param name="result"> Value for checking. </param>
        /// <returns> True if result is too big, false otherwise. </returns>
        private bool IsResultTooBig(string result)
        {
            if (!result.Contains(DECIMAL))
            {
                if (result.Length > MAX_DIGITS)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Stores result of the unary operation to proper register and displays it.
        /// </summary>
        /// <param name="unaryResult"> Result of an unary operation. </param>
        private void SetAndDisplayUnaryResult(string unaryResult)
        {
            string roundedResult = RoundResultIfNecessary(unaryResult);

            if (string.IsNullOrEmpty(_currentOperation))
            {
                bool shouldUseFirstOperand = string.IsNullOrEmpty(_secondOperand);

                if (shouldUseFirstOperand)
                {
                    _firstOperand = roundedResult;
                }
                else
                {
                    _secondOperand = roundedResult;
                }
            }
            else if (!string.IsNullOrEmpty(_secondOperand))
            {
                _secondOperand = roundedResult;
            }

            _currentDisplayState = roundedResult;
        }

        /// <summary>
        /// Gets second operand for execution of a binary operation.
        /// If second operand is not provided, first operand will become second as well.
        /// </summary>
        /// <param name="firstOperand"> First operand. </param>
        /// <returns> First operand if second is not provided, second otherwise. </returns>
        public double GetSecondBinaryOperand(double firstOperand)
        {
            if (string.IsNullOrEmpty(_secondOperand))
            {
                return firstOperand;
            }

            return double.Parse(_secondOperand);
        }

        /// <summary>
        /// Gets rounded result of the binary operation, clears registers and displays the result.
        /// </summary>
        /// <param name="calculatedValue"> Calculated result of the binary operation. </param>
        /// <returns> Rounded result of the binary operation. </returns>
        private string DisplayAndGetBinaryResult(string calculatedValue)
        {
            string result = RoundResultIfNecessary(calculatedValue);

            _currentDisplayState = result;
            _currentOperation = EMPTY;
            _secondOperand = EMPTY;

            return result;
        }

        /// <summary>
        /// Switches sign of a provided value.
        /// </summary>
        /// <param name="value"> Value for sign switching. </param>
        /// <returns> Rounded value with switched sign. </returns>
        private string SwitchSign(string value)
        {
            if (value.StartsWith(MINUS))
            {
                value = value.Substring(1);
            }
            else
            {
                value = value.Insert(0, MINUS);
            }

            return RoundResultIfNecessary(value);
        }

        /// <summary>
        /// Rounds provided value if necessary.
        /// Decimal separator and minus sign are allowed on top of MAX_DIGITS length.
        /// </summary>
        /// <param name="value"> Value for rounding. </param>
        /// <returns> Rounded value. </returns>
        private string RoundResultIfNecessary(string value)
        {
            bool isSigned = value.StartsWith(MINUS);

            if (value.Contains(DECIMAL))
            {
                int allowedLength = isSigned ? MAX_SIGNED_LENGTH : MAX_UNSIGNED_LENGTH;

                if (value.Length <= allowedLength)
                {
                    return value;
                }
                else
                {
                    char lastAllowedCharacter = value[allowedLength];
                    if (lastAllowedCharacter.Equals(DECIMAL))
                    {
                        return ERROR;
                    }

                    int lastDigit = int.Parse((lastAllowedCharacter).ToString());
                    if (lastDigit < ROUNDING_DIGIT)
                    {
                        return value.Substring(0, allowedLength);
                    }
                    else
                    {
                        int indexOfComma = value.IndexOf(DECIMAL);
                        int decimalPlaces = allowedLength - indexOfComma - 1;
                        decimal roundedNumber = Math.Round(decimal.Parse(value), decimalPlaces);
                        string roundedString = roundedNumber.ToString();
                        return roundedString.Substring(0, allowedLength);
                    }
                }
            }
            else
            {
                int allowedLength = isSigned ? MAX_SIGNED_LENGTH-1 : MAX_UNSIGNED_LENGTH-1;

                if (value.Length < allowedLength)
                {
                    return value;
                }
                else
                {
                    int lastDigit = int.Parse((value[allowedLength]).ToString());
                    if (lastDigit < ROUNDING_DIGIT)
                    {
                        return value.Remove(allowedLength);
                    }
                    else
                    {
                        string croppedValue = value.Remove(value.Length - 1);
                        int roundedNumber = int.Parse(croppedValue) + 1;
                        return roundedNumber.ToString();
                    }
                }
            }
        }

        /// <summary>
        /// Shows error on the display.
        /// </summary>
        private void ShowError()
        {
            _currentDisplayState = ERROR;
        }

        /// <summary>
        /// Checks if entered value is allowed in calculator.
        /// </summary>
        /// <param name="inPressedDigit"> New calculator entry. </param>
        /// <returns> True if entry is valid, false otherwise. </returns>
        private bool IsDigitValid(char inPressedDigit)
        {
            return char.IsDigit(inPressedDigit) || _binaryOperations.Contains(inPressedDigit) || inPressedDigit.Equals(EQUALS) ||
                   _unaryOperations.Contains(inPressedDigit) || _memoryOperations.Contains(inPressedDigit) ||
                   inPressedDigit.Equals(DECIMAL) || inPressedDigit.Equals(ON_OFF) || inPressedDigit.Equals(CLEAR);
        }

        /// <summary>
        /// Checks if calculator entry represents binary operation.
        /// </summary>
        /// <param name="inPressedDigit"> New calculator entry. </param>
        /// <returns> True if operation is binary, false otherwise. </returns>
        private bool IsBinaryOperation(char inPressedDigit)
        {
            return _binaryOperations.Contains(inPressedDigit);
        }

        /// <summary>
        /// Checks if calculator entry represents unary operation.
        /// </summary>
        /// <param name="inPressedDigit"> New calculator entry. </param>
        /// <returns> True if operation is unary, false otherwise. </returns>
        private bool IsUnaryOperation(char inPressedDigit)
        {
            return _unaryOperations.Contains(inPressedDigit);
        }

        /// <summary>
        /// Clears zero and decimal separator if number is in form "%d,0*".
        /// </summary>
        /// <param name="value"> Value for checking. </param>
        /// <returns> Cleared value. </returns>
        private string ClearPossibleZeroAfterDecimal(string value)
        {
            return Regex.Replace(value, ",0+$", EMPTY);
        }

        /// <summary>
        /// Enum which represents possible character types entered in calculator.
        /// </summary>
        private enum CharacterType
        {
            Number,
            UnaryOperation,
            BinaryOperation,
            MemoryOperation,
            Equals,
            Clear, 
            OnOff
        }

        /// <summary>
        /// Gets type of the character entered in a calculator.
        /// </summary>
        /// <param name="inPressedDigit"> New calculator entry. </param>
        /// <returns> Proper character type. </returns>
        private CharacterType GetCharacterType(char inPressedDigit)
        {
            CharacterType characterType;

            if (char.IsDigit(inPressedDigit) || inPressedDigit.Equals(DECIMAL))
            {
                characterType = CharacterType.Number;
            } else if (IsUnaryOperation(inPressedDigit))
            {
                characterType = CharacterType.UnaryOperation;
            } else if (IsBinaryOperation(inPressedDigit))
            {
                characterType = CharacterType.BinaryOperation;
            } else if (inPressedDigit.Equals(EQUALS))
            {
                characterType = CharacterType.Equals;
            }
            else if (inPressedDigit.Equals(CLEAR))
            {
                characterType = CharacterType.Clear;
            } else if (inPressedDigit.Equals(ON_OFF))
            {
                characterType = CharacterType.OnOff;
            } else
            {
                characterType = CharacterType.MemoryOperation;
            }

            return characterType;
        }
    }
}
