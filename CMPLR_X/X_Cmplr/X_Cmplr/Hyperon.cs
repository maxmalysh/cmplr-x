using System;
using System.Collections.Generic;
using System.Diagnostics;
using NM_GraphCommon;
using NM_DirectedGraphLibrary.NM_DirectedGraphLibraryInternals;

using NS_XCmplr;
using NS_Arch;

// Hyperon - гиперон. Частица из физики элементарных частиц

namespace NS_Hyperon
{
    /*
        "FrontEnd" Выход из FrontEnd-a
            Св-ва:
                - всё представление представление - один большой квазибазовый блок
                - присутствуют объекты классов производных от HQuasiBlockInstruction
                - все выражения представляют собой деревья произвольного вида
                - объекты HReg отсутствуют
     
        "CFG_Build" Построение CFG
            Св-ва:
                - всё представление - полноценный CFG (приведённый граф!)
                - присутствуют HGoTo, HCondGoTo. Объекты классов производных от HQuasiBlockInstruction удалены и 
                     заменены на соответсвующие HGoTo, HCondGoTo
                - все объекты в базовых блоках реализуют интерфейс IInstruction
     
        "SizeOf calculation"
                - заменить все sizeof на числа
     
        "ExplicitTypeCastInsertion"
            Св-ва:
                - для операторов присваивания типы слева и справа должны быть эквивалентны
                - для арифметических операций +,-,*,/ типы аргументов должны быть 
                            a) эквивалентны
                            b) целочисленным, вещественным или комплексным
                - для операций адресной арифметики +,- левый аргумент должен быть указатель, правый HIntegerType.PointerInteger
                - другие сво-ва (см. CTypeCheckerVisitor)
    
        "ExpressionReassociation"

        "ExpressionSimplification" 
                - все выражения приводятся к упрощённому (Simplified) виду. Появляются объекты HReg

        "RegGeneration" Замена NMO-объектов объектами HReg

        "SSA_Build" Построение SSA над объектами HReg
                - объекты HReg заменяются на объекты HSsaUseExpr/HSsaDefExpr
     */


    /// <summary>
    /// basic abstract class
    /// Любой элемент представления принадлежит классу HTree
    /// </summary>
    abstract class HTree 
    {
        abstract public void AcceptVisitor(CTreeVisitor v);
    }

    #region Data types

    /// <summary>
    /// basic data type class
    /// </summary>
    abstract class HType : HTree
    {
        public HType(int _size)
        {
            this._size = _size;
        }

        public virtual int Size
        {
            get
            {
                return _size;
            }
        }

        protected int _size;
    }

    class HVoidType : HType
    {
        static public void Init()
        {
            Debug.Assert(!_done);
            _done = true;
            _voidType = new HVoidType();
        }

        static public HVoidType VoidType
        { 
            get 
            { 
                return _voidType; 
            } 
        }

        private HVoidType()
            : base(0)
        { }

        static private bool _done = false;
        static private HVoidType _voidType = null;
    }

    class HIntegerType : HType 
    {
        static public void Init()
        {
            Debug.Assert(!_done);
            _done = true;

            _sInt8 = new HIntegerType(true, 1);
            _uInt8 = new HIntegerType(false, 1);
            _sInt16 = new HIntegerType(true, 2);
            _uInt16 = new HIntegerType(false, 2);
            _sInt32 = new HIntegerType(true, 4);
            _uInt32 = new HIntegerType(false, 4);
            _sInt64 = new HIntegerType(true, 8);
            _uInt64 = new HIntegerType(false, 8);
            _sInt128 = new HIntegerType(true, 16);
            _uInt128 = new HIntegerType(false, 16);
        }

        public bool IsSigned
        {
            get { return _isSigned; }
        }

        static public HIntegerType SInt8 
        {
            get { return _sInt8; }
        }

        static public HIntegerType UInt8
        {
            get { return _uInt8; }
        }

        static public HIntegerType SInt16
        {
            get { return _sInt16; }
        }

        static public HIntegerType UInt16
        {
            get { return _uInt16; }
        }

        static public HIntegerType SInt32
        {
            get { return _sInt32; }
        }

        static public HIntegerType UInt32
        {
            get { return _uInt32; }
        }

        static public HIntegerType SInt64
        {
            get { return _sInt64; }
        }

        static public HIntegerType UInt64
        {
            get { return _uInt64; }
        }

        static public HIntegerType SInt128
        {
            get { return _sInt128; }
        }

        static public HIntegerType UInt128
        {
            get { return _uInt128; }
        }

        static public HIntegerType PointerInteger
        {
            get { return (CArch.PointerSize == 32 ? HIntegerType.UInt32 : HIntegerType.UInt64); }
        }

        private HIntegerType(bool _isSigned, int _size)
            : base(_size)
        {
            this._isSigned = _isSigned;
        }

        private bool _isSigned;

        static private bool _done = false;
        static private HIntegerType _sInt8 = null;
        static private HIntegerType _uInt8 = null;
        static private HIntegerType _sInt16 = null;
        static private HIntegerType _uInt16 = null;
        static private HIntegerType _sInt32 = null;
        static private HIntegerType _uInt32 = null;
        static private HIntegerType _sInt64 = null;
        static private HIntegerType _uInt64 = null;
        static private HIntegerType _sInt128 = null;
        static private HIntegerType _uInt128 = null;
    }

    class HFloatType : HType 
    {
        static public HFloatType Float32
        {
            get { return _float32; }
        }

        static public HFloatType Float64
        {
            get { return _float64; }
        }

        static public HFloatType Float80
        {
            get { return _float80; }
        }

        private HFloatType(int _size)
            : base(_size)
        { }

        static public void Init()
        {
            Debug.Assert(!_done);
            _done = true;

            _float32 = new HFloatType(4);
            _float64 = new HFloatType(8);
            _float80 = new HFloatType(10);
        }

        static private bool _done = false;
        static private HFloatType _float32;
        static private HFloatType _float64;
        static private HFloatType _float80;
    }

    class HStructType : HType 
    {

        public HStructType()
            : base(0)
        {
            _fields = new List<HField>();
        }

        public List<HField> Fields
        {
            get { return _fields; }
        }

        public override int Size
        {
            get
            {
                if (_size == 0)
                {
                    HField [] fieldsArray = _fields.ToArray();
                    for (int i = 0; i < fieldsArray.Length; i++)
                    {
                        HField f = fieldsArray[i];

                        int fieldSize = f.Type.Size;
                        int alignSize = (i+1 < fieldsArray.Length ? fieldsArray[i+1].Align : 0);

                        int memLayotSize = (fieldSize / alignSize) * alignSize +
                                    (fieldSize / alignSize != 0 ? 1 : 0);

                        _size += memLayotSize;
                    }
                }
                return _size;
            }
        }

        public string Name
        {
            get 
            {
                // it's necessary name table
                return "";
            }
        }

        private List<HField> _fields;
    }

    class HComplexType : HType 
    {
        static public void Init()
        {
            Debug.Assert(!_done);
            _done = true;

            _complexType32 = new HComplexType(HFloatType.Float32);
            _complexType64 = new HComplexType(HFloatType.Float64);
            _complexType80 = new HComplexType(HFloatType.Float80);
        }

        static public HComplexType Complex32
        {
            get { return _complexType32; }
        }

        static public HComplexType Complex64
        {
            get { return _complexType64; }
        }

        static public HComplexType Complex80
        {
            get { return _complexType80; }
        }

        private HComplexType(HType _baseType)
            : base(0)
        {
            this._baseType = _baseType;
        }

        public HType BaseType
        {
            get { return _baseType; }
        }

        public override int Size
        {
            get
            {
                if (_size == 0)
                {
                    int fieldSize = _baseType.Size;
                    int alignSize = COptions.AlignStructField;
                    
                    int memLayotSize = (fieldSize / alignSize) * alignSize +
                                    (fieldSize / alignSize != 0 ? 1 : 0);

                    _size += memLayotSize;
                    _size += fieldSize;
                }
                return _size;
            }
        }

        private HType _baseType;
        static private bool _done = false;
        static private HComplexType _complexType32 = null;
        static private HComplexType _complexType64 = null;
        static private HComplexType _complexType80 = null;
    }

    class HBooleanType : HType 
    {
        static public void Init()
        {
            Debug.Assert(!_done);
            _done = true;
        }

        static public HBooleanType Boolean
        {
            get { return _booleanType; }
        }

        private HBooleanType()
            : base(CArch.BooleanSize)
        { }

        static private bool _done = false;
        static private HBooleanType _booleanType = null;
    }

    class HPointerType : HType
    {
        public HPointerType(HType _baseType)
            : base(CArch.PointerSize)
        {
            this._baseType = _baseType;
        }

        public HType BaseType
        {
            get { return _baseType; }
        }

        private HType _baseType;
    }

    class HArrayType : HType 
    {
        public HArrayType(HType _baseType, int _itemNum)
            : base(_itemNum)
        {
            this._baseType = _baseType;
        }

        public HType BaseType
        {
            get { return _baseType; }
        }

        public int ItemNum
        {
            get { return _itemNum; }
        }

        public override int Size
        {
            get
            {
                if (_size == 0)
                {
                    int itemSize = _baseType.Size;
                    int align = COptions.AlignArrayItem;
                    int sizeMemLayout = (itemSize / align) * align + (itemSize % align != 0 ? 1 : 0) * align;
                    _size = _itemNum * sizeMemLayout;
                }
                return _size;
            }
        }

        private HType _baseType;
        private int _itemNum;
    }

    class HFunType : HType
    {
        public HFunType(HType _resType, HType [] _paramTypes)
           : base(0)
        {
            this._resType = _resType;
            this._paramTypes = _paramTypes;
        }

        public HType ResType
        {
            get { return _resType; }
        }

        public HType [] ParamTypes
        {
            get { return _paramTypes; }
        }

        private HType _resType;
        private HType [] _paramTypes;
    }

    #endregion

    // label
    class HLabel : HTree 
    {
        public string Name
        {
            get 
            {
                // name table
                return "";
            }
        }
    }

    #region Expressions

    // expressions
    abstract class HExpr : HTree
    {
        public abstract HType Type { get; }
    }

    #region Expressions / Named memory objects (NMOs)

    // declarations
    /// <summary>
    /// В данном 
    /// </summary>
    abstract class HNmo : HExpr
    {
        public HType Type
        {
            get { return _type; }
        }

        public HMemDscr MemDscr
        {
            get { return _memDscr; }
        }

        public abstract string Name { get; }

        protected HType _type;
        protected HMemDscr _memDscr;
    }

    class HVarNmo : HNmo
    {
        public HVarNmo(HType _type, HMemDscr _memDscr)
        {
            Debug.Assert(!(_type is HFunType));

            this._type = _type;
            this._memDscr = _memDscr;
        }

        public override string Name
        {
            get { throw new NotImplementedException(); }
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            throw new NotImplementedException();
        }
    }

    class HParamNmo : HNmo
    {
        public HParamNmo(HType _type, HMemDscr _memDscr, HFunNmo _funNmo)
        {
            Debug.Assert(!(_type is HFunType));

            this._type = _type;
            this._memDscr = _memDscr;
            this._funNmo = _funNmo;
        }

        public HFunNmo FunNmo
        {
            get { return _funNmo; }
        }

        public override string Name
        {
            get { throw new NotImplementedException(); }
        }

        private HFunNmo _funNmo;
    }

    class HFunNmo : HNmo
    {
        public HFunNmo(HFunType _funType)
        {
            this._type = _funType;
        }

        public HFunType FunType
        {
            get
            {
                return _type as HFunType;
            }
        }

        public override string Name
        {
            get { throw new NotImplementedException(); }
        }
    }

    #endregion

    #region Expressions / constants

    // constants
    abstract class HConstExpr : HExpr
    {
        public override HType Type
        {
            get { return _type; }
        }

        public object Value
        {
            get { return _value; }
        }

        protected HType _type;
        protected object _value;
    }

    class HIntegerCstExpr : HConstExpr
    {
        public HIntegerCstExpr(sbyte _value)
        {
            this._type = HIntegerType.SInt8;
            this._value = _value;
        }

        public HIntegerCstExpr(byte _value)
        {
            this._type = HIntegerType.UInt8;
            this._value = _value;
        }

        public HIntegerCstExpr(Int16 _value)
        {
            this._type = HIntegerType.SInt16;
            this._value = _value;
        }

        public HIntegerCstExpr(UInt16 _value)
        {
            this._type = HIntegerType.UInt16;
            this._value = _value;
        }

        public HIntegerCstExpr(Int32 _value)
        {
            this._type = HIntegerType.SInt32;
            this._value = _value;
        }

        public HIntegerCstExpr(UInt32 _value)
        {
            this._type = HIntegerType.UInt32;
            this._value = _value;
        }

        public HIntegerCstExpr(Int64 _value)
        {
            this._type = HIntegerType.SInt64;
            this._value = _value;
        }

        public HIntegerCstExpr(UInt64 _value)
        {
            this._type = HIntegerType.UInt64;
            this._value = _value;
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.IntegerCstExprVisit(this);
        }
    }

    class HFloatCstExpr : HConstExpr 
    {
        public HFloatCstExpr(float _value)
        {
            this._type = HFloatType.Float32;
            this._value = _value;
        }

        public HFloatCstExpr(double _value)
        {
            this._type = HFloatType.Float64;
            this._value = _value;
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.FloatCstExprVisit(this);
        }
    }

    class HComplexCstExpr : HConstExpr
    {
        struct HComplexValue
        {
            public object Re;
            public object Im;
        }

        public HComplexCstExpr(HComplexType _type, HComplexValue _value)
        {
            Debug.Assert(_type == HComplexType.Complex32 || _type == HComplexType.Complex64);
            this._value = _value;
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.ComplexCstExprVisit(this);
        }
    }

    #endregion

    #region Expressions / Assigns

    // more complex expressions
    class HAssignExpr : HExpr, IInstruction
    {
        public HAssignExpr(HExpr _res, HExpr _right)
        {
            this._res = _res;
            this._right = _right;
        }

        public HExpr Res
        {
            get { return _res; }
        }

        public HExpr Right
        {
            get { return _right; }
        }

        public override HType Type
        {
            get { return _res.Type; }
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.AssignExprVisit(this);
            Res.AcceptVisitor(v);
            Right.AcceptVisitor(v);
        }

        public HBasicBlock BasicBlock
        {
            get { return _basicBlock; }
            set { _basicBlock = value; }
        }

        private HExpr _res;
        private HExpr _right;
        private HBasicBlock _basicBlock;
    }

    #endregion

    #region Expressions / unary expressions

    // unary expressions
    abstract class HUnaryExpr : HExpr
    {
        public HUnaryExpr(HExpr _operand)
        {
            this._operand = _operand;
        }

        public HExpr Operand
        {
            get 
            { 
                return _operand; 
            }
        }

        protected HExpr _operand;
    }

    class HSizeOfExpr : HUnaryExpr
    { 
        public HSizeOfExpr(HExpr _op)
            : base(_op)
        {
            this._toBeMeasured = null;
        }

        public HSizeOfExpr(HType _typeToBeMeasured)
            : base(null)
        {
            this._toBeMeasured = _typeToBeMeasured;
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.SizeOfExprVisit(this);
            if (_operand != null)
                _operand.AcceptVisitor(v);
        }

        public override HType Type
        {
            get 
            {
                return HIntegerType.PointerInteger;
            }
        }

        public HType TypeToBeMeasured
        {
            get { return _toBeMeasured; }
        }

        private HType _toBeMeasured;
    }

    class HNegateExpr : HUnaryExpr 
    {
        public HNegateExpr(HExpr _op)
            : base(_op)
        { }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.NegateExprVisit(this);
            _operand.AcceptVisitor(v);
        }

        public override HType Type
        {
            get 
            {
                return _operand.Type;
            }
        } 
    }

    class HBitNotExpr : HUnaryExpr 
    {
        public HBitNotExpr(HExpr _op)
            : base(_op)
        { }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.BitNotExprVisit(this);
            _operand.AcceptVisitor(v);
        }

        public override HType Type
        {
            get
            {
                return _operand.Type;
            }
        } 
    }

    class HBoolNotExpr : HUnaryExpr 
    { 
        public HBoolNotExpr(HExpr _op)
            : base(_op)
        { }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.BoolNotExprVisit(this);
            _operand.AcceptVisitor(v);
        }

        public override HType Type
        {
            get
            {
                return HBooleanType.Boolean;
            }
        } 
    }

    class HStepExpr : HUnaryExpr 
    {
        public enum EOpKind
        {
            PRE_INCREMENT,
            POST_INCREMENT,
            PRE_DECREMENT,
            POST_DECREMENT
        }

        public HStepExpr(HExpr _op, EOpKind _kind)
            : base(_op)
        {
            this._kind = _kind;
        }

        public EOpKind Kind
        {
            get { return _kind; }
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.StepExprVisit(this);
            _operand.AcceptVisitor(v);
        }

        public override HType Type
        {
            get
            {
                return _operand.Type;
            }
        } 

        private EOpKind _kind;
    }

    class HComplexConjExpr : HUnaryExpr 
    {
        public HComplexConjExpr(HExpr _op)
            : base(_op)
        { }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.ComplexConjExprVisit(this);
            _operand.AcceptVisitor(v);
        }

        public override HType Type
        {
            get
            {
                return _operand.Type;
            }
        } 
    }

    class HComplexReExpr : HUnaryExpr 
    { 
        public HComplexReExpr(HExpr _op)
            : base(_op)
        { }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.ComplexReExprVisit(this);
            _operand.AcceptVisitor(v);
        }

        public override HType Type
        {
            get 
            {
                HType type = _operand.Type;
                return (type as HComplexType).BaseType;
            }
        }
    }

    class HComplexImExpr : HUnaryExpr 
    {
        public HComplexImExpr(HExpr _op)
            : base(_op)
        { }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.ComplexImExprVisit(this);
            _operand.AcceptVisitor(v);
        }

        public override HType Type
        {
            get 
            {
                HType type = _operand.Type;
                return (type as HComplexType).BaseType;
            }
        }
    }

    abstract class HCastToExpr : HUnaryExpr
    {
        public HCastToExpr(HExpr _op)
            : base(_op)
        { }


        public override HType Type
        {
            get
            {
                return _toType;
            }
        }

        protected HType _toType;
    }

    class HCastToFloatExpr : HCastToExpr
    {
        public HCastToFloatExpr(HExpr _op, HType _toType)
            : base(_op)
        {
            this._toType = _toType;
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.CastToFloatExprVisit(this);
            _operand.AcceptVisitor(v);
        }
    }

    class HCastToIntExpr : HCastToExpr
    { 
        public HCastToIntExpr(HExpr _op, HType _toType)
            : base(_op)
        {
            this._toType = _toType;
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.CastToIntExprVisit(this);
            _operand.AcceptVisitor(v);
        }
    }

    class HCastToPointerExpr : HCastToExpr
    {
        public HCastToPointerExpr(HExpr _op, HType _toType)
            : base(_op)
        {
            this._toType = _toType;
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.CastToPointerExprVisit(this);
            _operand.AcceptVisitor(v);
        }
    }

    #endregion

    #region Expressions / Binary expressions

    // binary expressions
    abstract class HBinaryExpr : HExpr
    {
        public HBinaryExpr(HExpr _left, HExpr _right)
        {
            this._left = _left;
            this._right = _right;
        }

        public override HType Type
        {
            get 
            {
                return this._left.Type;
            }
        }

        public HExpr Left
        {
            get { return _left; }
        }

        public HExpr Right
        {
            get { return _right; }
        }

        protected HExpr _left;
        protected HExpr _right;
    }

    class HBinaryBitOpExpr : HBinaryExpr 
    {
        public enum EOpKind
        {
            LSHIFT,
            RSHIFT,
            OR,
            XOR,
            AND
        }

        public HBinaryBitOpExpr(HExpr _left, HExpr _right, EOpKind _kind)
            : base(_left, _right)
        {
            this._kind = _kind;
        }

        public EOpKind Kind
        {
            get { return _kind; }
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.BinaryBitOpExprVisit(this);
            _left.AcceptVisitor(v);
            _right.AcceptVisitor(v);
        }

        private EOpKind _kind;
    }

    class HBinaryBoolOpExpr : HBinaryExpr
    {
        public enum EOpKind
        {
            AND_IF,  // short circuit scheme support
            OR_IF,   // short circuit scheme support
            AND,
            OR
        }

        public HBinaryBoolOpExpr(HExpr _left, HExpr _right, EOpKind _kind)
            : base(_left, _right)
        {
            this._kind = _kind;
        }

        public EOpKind Kind
        {
            get { return _kind; }
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.BinaryBoolOpExprVisit(this);
            _left.AcceptVisitor(v);
            _right.AcceptVisitor(v);
        }

        private EOpKind _kind;
    }

    class HBinarySafeArithOpExpr : HBinaryExpr 
    {
        public enum EOpKind
        {
            PLUS,
            MINUS,
            MULT
        }

        public HBinarySafeArithOpExpr(HExpr _left, HExpr _right, EOpKind _kind)
            : base(_left, _right)
        {
            this._kind = _kind;
        }

        public EOpKind Kind
        {
            get { return _kind; }
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.BinarySafeArithOpExprVisit(this);
            _left.AcceptVisitor(v);
            _right.AcceptVisitor(v);
        }

        private EOpKind _kind;
    }

    class HBinaryPointerArithOpExpr : HBinaryExpr
    {
        public enum EOpKind
        {
            PLUS,
            MINUS
        }

        public HBinaryPointerArithOpExpr(HExpr _left, HExpr _right, EOpKind _kind)
            : base(_left, _right)
        {
            this._kind = _kind;
        }

        public EOpKind Kind
        {
            get { return _kind; }
        }

        public override HType Type
        {
            get
            {
                return _left.Type is HPointerType ? _left.Type : _right.Type;
            }
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.BinaryPointerArithOpExprVisit(this);
            _left.AcceptVisitor(v);
            _right.AcceptVisitor(v);
        }

        private EOpKind _kind;
    }

    // float division
    class HFloatDivExpr : HBinaryExpr 
    {
        public HFloatDivExpr(HExpr _left, HExpr _right)
            : base(_left, _right)
        { }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.FloatDivExprVisit(this);
            _left.AcceptVisitor(v);
            _right.AcceptVisitor(v);
        }
    }

    // complex number division
    class HComplexDivExpr : HBinaryExpr
    {
        public HComplexDivExpr(HExpr _left, HExpr _right)
            : base(_left, _right)
        { }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.ComplexDivExprVisit(this);
            _left.AcceptVisitor(v);
            _right.AcceptVisitor(v);
        }
    }

    class HIntDivOpExpr : HBinaryExpr
    {
        public enum EOpKind
        {
            DIV_TRUNC,      // int division, round to 0
            DIV_FLOOR,      // int division, round to -inf
            DIV_CEIL,       // int division, round to +inf
            DIV_ROUND,      // int division, round to the nearest integer
            MOD_TRUNC,      // int mod, round to 0
            MOD_FLOOR,      // int mod, round to -inf
            MOD_CEIL,       // int mod, round to +inf
            MOD_ROUND       // int mod, round to the nearest integer
        }

        public HIntDivOpExpr(HExpr _left, HExpr _right, EOpKind _kind)
            : base(_left, _right)
        {
            this._kind = _kind;
        }

        public EOpKind Kind
        {
            get { return _kind; }
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.IntDivOpExprVisit(this);
            _left.AcceptVisitor(v);
            _right.AcceptVisitor(v);
        }

        private EOpKind _kind;
    }

    class HCmpExpr : HBinaryExpr
    {
        public enum EOpKind
        {
            LT,
            LE,
            GT,
            GE,
            EQ,
            NE
        }

        public HCmpExpr(HExpr _left, HExpr _right, EOpKind _kind)
            : base(_left, _right)
        {
            this._kind = _kind;
        }

        public override HType Type
        {
            get
            {
                return HBooleanType.Boolean;
            }
        }

        public EOpKind Kind
        {
            get { return _kind; }
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.CmpExprVisit(this);
            _left.AcceptVisitor(v);
            _right.AcceptVisitor(v);
        }

        private EOpKind _kind;
    }

    #endregion

    #region Call expression

    // call expression
    class HCallNonVoidFunExpr : HExpr
    {
        public HCallNonVoidFunExpr(HExpr _toBeCalled, HExpr[] _params)
        {
            this._toBeCalled = _toBeCalled;
            this._params = _params;
        }

        public HExpr ToBeCalled
        {
            get { return _toBeCalled; }
        }

        public HExpr [] Params
        {
            get { return _params; }
        }

        public override HType Type
        {
            get 
            {
                return (_toBeCalled.Type as HFunType).ResType;
            }
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.CallNonVoidFunExprVisit(this);
            _toBeCalled.AcceptVisitor(v);
            foreach (HExpr p in _params)
            {
                p.AcceptVisitor(v);
            }
        }

        protected HExpr _toBeCalled;
        protected HExpr [] _params;
    }

    class HCallVoidFunExpr : HCallNonVoidFunExpr, IInstruction
    {
        public HCallVoidFunExpr(HExpr _toBeCalled, HExpr[] _params)
            : base(_toBeCalled, _params)
        { }

        public HBasicBlock BasicBlock
        {
            get { return _basicBlock; }
            set { _basicBlock = value; }
        }

        public override void AcceptVisitor(CTreeVisitor v)
        {
            v.CallVoidFunExprVisit(this);
            _toBeCalled.AcceptVisitor(v);
            foreach (HExpr p in _params)
            {
                p.AcceptVisitor(v);
            }
        }

        protected HBasicBlock _basicBlock;
    }

    #endregion

    #region Memory expression

    abstract class HMemExpr : HExpr
    {
        public HMemDscr MemDscr
        {
            get { return _memDscr; }
        }

        protected HMemDscr _memDscr;
    }

    class HArrayRefExpr : HMemExpr 
    {
        public HArrayRefExpr(HExpr _array, HExpr _index, HMemDscr _memDscr)
        {
            this._array = _array;
            this._index = _index;
            this._memDscr = _memDscr;
        }

        public HExpr Array
        {
            get { return _array; }
        }

        public HExpr Index
        {
            get { return _index; }
        }

        private HExpr _array;
        private HExpr _index;
    }

    class HStarExpr : HMemExpr 
    {
        public HStarExpr(HExpr _pointer)
        {
            this._pointer = _pointer;
        }

        public override HType Type
        {
            get 
            { 
                return (_pointer.Type as HPointerType).BaseType; 
            }
        }

        public HExpr Pointer
        {
            get { return _pointer; }
        }

        private HExpr _pointer;
    }

    class HStructAccessExpr : HMemExpr 
    {
        public HStructAccessExpr(HExpr _struct, HExpr _field)
        {
            this._struct = _struct;
            this._field = _field;
        }

        public HExpr Struct
        {
            get { return _struct; }
        }

        public HExpr Field
        {
            get { return _field; }
        }

        public override HType Type
        {
            get { return _field.Type; }
        }

        private HExpr _struct;
        private HExpr _field;
    }

    #endregion

    #region Expressions / struct field expression

    public class HField : HExpr
    {
        public HField(HStructType _structType, HType _type, int _align)
        {
            this._structType = _structType;
            this._type = _type;
            this._align = _align;
        }

        public int Align
        {
            get { return _align; }
        }

        public HStructType StructType
        {
            get { return _structType; }
        }

        public HType Type
        {
            get { return _type; }
        }

        public string Name
        {
            get
            {
                // table name should be implemented
                return "";
            }
        }

        private int _align;
        private HType _type;
        private HStructType _structType;
    }

    #endregion

    #endregion

    #region SSA support

    // SSA_SUPPORT
    class HReg : HExpr 
    {
        public HReg(HType _type)
        {
            Debug.Assert(!(_type is HFunType));

            this._type = _type;
            this._index = _tempCounter++;
            this._version = 0;
        }

        static public HReg CreateNewVersion(HReg _original, int _version)
        {
            HReg _reg = new HReg();
            _reg._type = _original._type;
            _reg._index = _original._index;
            _reg._version = _version;
            return _reg;
        }

        public override HType Type
        {
            get { return _type; }
        }

        public int Index
        {
            get { return _index; }
        }

        public int Version
        {
            get { return _version; }
        }

        public bool IsVersioned
        {
            get { return (_version != 0); }
        }

        private HReg()
        { }

        private HType _type;
        private int _index;
        private int _version;
        static private int _tempCounter = 0;
    }

    // SSA_SUPPORT
    class HPhiExpr : HExpr, IInstruction
    {
        public HBasicBlock BasicBock
        {
            get { return _basicBlock; }
            set { _basicBlock = value; }
        }

        public HSsaDefExpr Res
        {
            get { return _res; }
            set { _res = value; }
        }

        public List<HSsaUseExpr> Uses
        {
            get { return _uses; }
        }

        public HSsaUseExpr GetUseByEdge(HEdge e)
        {
            // ...
            return null;
        }

        public HEdge GetEdgeByUse(HSsaUseExpr use)
        {
            // ...
            return null;
        }

        private HSsaDefExpr _res;
        private List<HSsaUseExpr> _uses;
        private HBasicBlock _basicBlock;
    }

    // SSA_SUPPORT
    class HSsaUseExpr : HExpr 
    {
        public HSsaUseExpr(HSsaDefExpr _def)
        {
            Debug.Assert(_def.Reg.IsVersioned);

            this._def = _def;
        }

        public HReg Reg
        {
            get { return _def.Reg; }
        }

        public override HType Type
        {
            get { return _def.Type; }
        }

        private HSsaDefExpr _def;
    }

    // SSA_SUPPORT
    class HSsaDefExpr : HExpr 
    {
        public HSsaDefExpr(HReg _reg)
        {
            Debug.Assert(_reg.IsVersioned);

            this._reg = _reg;
        }

        public HReg Reg
        {
            get { return _reg; }
        }

        public List<HSsaUseExpr> Uses
        {
            get { return _uses; }
        }

        public override HType Type
        {
            get { return _reg.Type; }
        }

        private HReg _reg;
        private List<HSsaUseExpr> _uses;
    }

    #endregion

    #region Operand iterator

    // SSA_SUPPORT
    class COperandEnumerators
    {
        // обход всех операндов
        // обход всех операндов, которые являются HSsaUseExpr 
        // обход всех операндов, которые являются HReg
        // обход всех операндов, которые являются НNmoExpr
    }

    #endregion

    #region Memory descriptor

    // Memory descriptor
    enum EMemClass
    {
        STATIC,
        STACK,
        HEAP
    }

    class HMemDscr
    {
        public HMemDscr(EMemClass _memClass, int _align, int _size)
        {
            this._memClass = _memClass;
            this._align = _align;
            this._size = _size;
        }

        public EMemClass MemClass
        {
            get { return _memClass; }
        }

        public int Align 
        {
            get { return _align; }
        }

        public int Size
        {
            get { return _size; }
        }

        private EMemClass _memClass;
        private int _align;
        private int _size;
    }

    #endregion

    #region Control flow statements

    abstract class HQuasiBlockInstruction : HTree
    { }

    class HLabeledGoto : HQuasiBlockInstruction
    {
        public HLabeledGoto(HLabel _target)
        {
            this._target = _target;
        }

        private HLabel _target;
    }

    class HLabeledCondGoto : HQuasiBlockInstruction
    {
        public HLabeledCondGoto(HLabel _targetTrue, HLabel _targetFalse)
        {
            this._targetFalse = _targetFalse;
            this._targetTrue = _targetTrue;
        }

        private HLabel _targetTrue;
        private HLabel _targetFalse;
    }

    class HIf : HQuasiBlockInstruction { }
    class HThen : HQuasiBlockInstruction { }
    class HElse : HQuasiBlockInstruction { }
    class HBegin : HQuasiBlockInstruction { }
    class HEnd : HQuasiBlockInstruction { }
    class HWhile : HQuasiBlockInstruction { }
    class HDo : HQuasiBlockInstruction { }
    class HFor : HQuasiBlockInstruction { }
    class HSwitch : HQuasiBlockInstruction { }
    class HCase : HQuasiBlockInstruction { }

    class HGoto : HTree, IInstruction
    {
        public HGoto(HBasicBlock _target, HBasicBlock _bb)
        {
            this._target = _target;
            this._bb = _bb;
        }

        public HBasicBlock Target
        {
            get { return _target; }
            set { _target = value; }
        }

        public HBasicBlock BasicBlock
        {
            get { return _bb; }
        }

        private HBasicBlock _target;
        private HBasicBlock _bb;
    }

    class HCondGoto : HTree, IInstruction
    {
        public HCondGoto(HBasicBlock _targetTrue, HBasicBlock _targetFalse, HBasicBlock _bb)
        {
            this._targetFalse = _targetFalse;
            this._targetTrue = _targetTrue;
        }

        public HBasicBlock TargetFalse
        {
            get { return _targetFalse; }
            set { _targetFalse = value; }
        }

        public HBasicBlock TargetTrue
        {
            get { return _targetTrue; }
            set { _targetTrue = value; }
        }

        public HBasicBlock BasicBlock
        {
            get { return _bb; }
        }

        private HBasicBlock _targetTrue;
        private HBasicBlock _targetFalse;
        private HBasicBlock _bb;
    }

    #endregion

    #region Control Flow Graph

    interface IInstruction
    {
        HBasicBlock BasicBlock { get; set; }
    }

    class HBasicBlock
    {
        public HBasicBlock()
        {
            _bbId = HBasicBlock._basicBlockCounter++;
        }

        public List<IInstruction> Insts
        {
            get { return _insts; }
        }

        private List<IInstruction> _insts;
        private int _bbId;
        static private int _basicBlockCounter = 0;
    }

    class HEdge
    {
        public HEdge(bool _label)
        {
            this._label = _label;
        }

        public bool Label
        {
            get { return _label; }
        }

        private bool _label;
    }

    public class HCfg<HEdge, HBasicBlock> : CDirGraph<HEdge, HBasicBlock>
    {
        public HCfg()
            : base(new CDirectedAdjListStorage<HEdge, HBasicBlock>())
        { }
    }

    #endregion

    #region Visitors

    abstract class CTreeVisitor
    {
        #region Constants

        public virtual void IntegerCstExprVisit(HIntegerCstExpr t) { }
        public virtual void FloatCstExprVisit(HFloatCstExpr t) { }
        public virtual void ComplexCstExprVisit(HComplexCstExpr t) { }

        #endregion

        #region Assign

        public virtual void AssignExprVisit(HAssignExpr t) { }

        #endregion

        #region Unary expressions

        public virtual void SizeOfExprVisit(HSizeOfExpr t) { }
        public virtual void NegateExprVisit(HNegateExpr t) { }
        public virtual void BitNotExprVisit(HBitNotExpr t) { }
        public virtual void BoolNotExprVisit(HBoolNotExpr t) { }
        public virtual void StepExprVisit(HStepExpr t) { }
        public virtual void ComplexConjExprVisit(HComplexConjExpr t) { }
        public virtual void ComplexReExprVisit(HComplexReExpr t) { }
        public virtual void ComplexImExprVisit(HComplexImExpr t) { }
        public virtual void CastToFloatExprVisit(HCastToFloatExpr t) { }
        public virtual void CastToIntExprVisit(HCastToIntExpr t) { }
        public virtual void CastToPointerExprVisit(HCastToPointerExpr t) { }

        #endregion

        #region Binary expressions

        public virtual void BinaryBitOpExprVisit(HBinaryBitOpExpr t) { }
        public virtual void BinaryBoolOpExprVisit(HBinaryBoolOpExpr t) { }
        public virtual void BinarySafeArithOpExprVisit(HBinarySafeArithOpExpr t) { }
        public virtual void BinaryPointerArithOpExprVisit(HBinaryPointerArithOpExpr t) { }
        public virtual void FloatDivExprVisit(HFloatDivExpr t) { }
        public virtual void ComplexDivExprVisit(HComplexDivExpr t) { }
        public virtual void IntDivOpExprVisit(HIntDivOpExpr t) { }
        public virtual void CmpExprVisit(HCmpExpr t) { }

        #endregion

        #region Call expression

        public virtual void CallNonVoidFunExprVisit(HCallNonVoidFunExpr t) { }
        public virtual void CallVoidFunExprVisit(HCallVoidFunExpr t) { }

        #endregion

        #region Memory expressions

        public virtual void ArrayRefExprVisit(HArrayRefExpr t) { }
        public virtual void StarExprVisit(HStarExpr t) { }
        public virtual void StructAccessExprVisit(HStructAccessExpr t) { }

        #endregion
    }

    class CTypeCheckerVisitor : CTreeVisitor
    {
        // no checks in constants

        #region Assign

        public override void AssignExprVisit(HAssignExpr t) 
        {
            Debug.Assert(_areTypesEquivalent(t.Res.Type, t.Right.Type));
        }

        #endregion

        #region Unary expressions

        public override void SizeOfExprVisit(HSizeOfExpr t) 
        {
            Debug.Assert(
                            (t.Operand == null && t.Type != null) ||
                            (t.Operand != null && t.Type == null) 
                        );
        }

        public override void NegateExprVisit(HNegateExpr t) 
        {
            HType type = t.Operand.Type;
            Debug.Assert(type is HIntegerType || type is HFloatType || type is HComplexType);
        }

        public override void BitNotExprVisit(HBitNotExpr t)
        {
            HType type = t.Operand.Type;
            Debug.Assert(type is HIntegerType);
        }

        public override void BoolNotExprVisit(HBoolNotExpr t)
        {
            HType type = t.Operand.Type;
            Debug.Assert(type is HBooleanType);
        }

        public override void StepExprVisit(HStepExpr t)
        {
            HType type = t.Operand.Type;
            Debug.Assert(type is HIntegerType || type is HFloatType);
        }

        public override void ComplexConjExprVisit(HComplexConjExpr t)
        {
            HType type = t.Operand.Type;
            Debug.Assert(type is HComplexType);
        }

        public override void ComplexReExprVisit(HComplexReExpr t)
        {
            HType type = t.Operand.Type;
            Debug.Assert(type is HComplexType);
        }

        public override void ComplexImExprVisit(HComplexImExpr t)
        {
            HType type = t.Operand.Type;
            Debug.Assert(type is HComplexType);
        }

        public override void CastToFloatExprVisit(HCastToFloatExpr t)
        {
            HType type = t.Operand.Type;
            Debug.Assert(type is HIntegerType || type is HFloatType);
            Debug.Assert(t.Type is HFloatType);
        }

        public override void CastToIntExprVisit(HCastToIntExpr t)
        {
            HType type = t.Operand.Type;
            Debug.Assert(type is HIntegerType || type is HFloatType);
            Debug.Assert(t.Type is HIntegerType);
        }

        public override void CastToPointerExprVisit(HCastToPointerExpr t)
        {
            HType type = t.Operand.Type;
            Debug.Assert(type is HPointerType);
            Debug.Assert(t.Type is HPointerType);
        }

        #endregion

        #region Binary expressions

        public override void BinaryBitOpExprVisit(HBinaryBitOpExpr t)
        {
            Debug.Assert(_areTypesEquivalent(t));
            Debug.Assert(t.Type is HIntegerType);
        }

        public override void BinaryBoolOpExprVisit(HBinaryBoolOpExpr t)
        {
            Debug.Assert(_areTypesEquivalent(t));
            Debug.Assert(t.Type is HBooleanType);
        }

        public override void BinarySafeArithOpExprVisit(HBinarySafeArithOpExpr t) 
        {
            Debug.Assert(_areTypesEquivalent(t));
            Debug.Assert(t.Type is HIntegerType || t.Type is HFloatType || t.Type is HComplexType);
        }

        public override void BinaryPointerArithOpExprVisit(HBinaryPointerArithOpExpr t) 
        {
            Debug.Assert(t.Left.Type is HPointerType);
            Debug.Assert(t.Right.Type == HIntegerType.PointerInteger);
        }

        public override void FloatDivExprVisit(HFloatDivExpr t) 
        {
            Debug.Assert(_areTypesEquivalent(t));
            Debug.Assert(t.Type is HFloatType);
        }

        public override void ComplexDivExprVisit(HComplexDivExpr t) 
        {
            Debug.Assert(_areTypesEquivalent(t));
            Debug.Assert(t.Type is HComplexType);
        }

        public override void IntDivOpExprVisit(HIntDivOpExpr t) 
        {
            Debug.Assert(_areTypesEquivalent(t));
            Debug.Assert(t.Type is HIntegerType);
        }

        #endregion

        #region Call expression

        public override void CallNonVoidFunExprVisit(HCallNonVoidFunExpr t) 
        {
            _checkFunParams(t);
            Debug.Assert(t.Type != HVoidType.VoidType);
        }

        public override void CallVoidFunExprVisit(HCallVoidFunExpr t) 
        {
            _checkFunParams(t);
            Debug.Assert(t.Type == HVoidType.VoidType);
        }

        #endregion

        #region Memory expression

        // memory references
        public override void ArrayRefExprVisit(HArrayRefExpr t) 
        {
            Debug.Assert(t.Array.Type is HArrayType);

            HType indexType = t.Index.Type;
            HType signedTypeIndex = CArch.PointerSize == 32 ? HIntegerType.SInt32 : HIntegerType.SInt64;
            HType unSignedTypeIndex = CArch.PointerSize == 32 ? HIntegerType.UInt32 : HIntegerType.UInt64;

            Debug.Assert(indexType == signedTypeIndex || indexType == unSignedTypeIndex);
        }

        public override void StarExprVisit(HStarExpr t) 
        {
            Debug.Assert(t.Pointer.Type is HPointerType);
        }

        public override void StructAccessExprVisit(HStructAccessExpr t) 
        {
            Debug.Assert(t.Struct.Type is HStructType);
            Debug.Assert(t.Field is HField);
            Debug.Assert((t.Field as HField).StructType == t.Struct.Type);
        }

        #endregion

        #region Aux methods

        static private bool _areTypesEquivalent(HType t1, HType t2)
        {
            if (t1 == t2)
            {
                return true;
            }
            else if (t1 is HComplexType && t2 is HComplexType)
            {
                return (t1 as HComplexType).BaseType == (t2 as HComplexType).BaseType;
            }
            else
            {
                if (t1 is HPointerType && t2 is HPointerType)
                {
                    HPointerType dt1 = t1 as HPointerType;
                    HPointerType dt2 = t2 as HPointerType;
                    return _areTypesEquivalent(dt1.BaseType, dt2.BaseType);
                }
                else if (t1 is HArrayType && t2 is HArrayType)
                {
                    HArrayType dt1 = t1 as HArrayType;
                    HArrayType dt2 = t2 as HArrayType;
                    return _areTypesEquivalent(dt1.BaseType, dt2.BaseType);
                }
                else if (t1 is HStructType && t2 is HStructType)
                {
                    HStructType dt1 = t1 as HStructType;
                    HStructType dt2 = t2 as HStructType;

                    HField [] fields1 = dt1.Fields.ToArray();
                    HField [] fields2 = dt2.Fields.ToArray();

                    if (fields1.Length != fields2.Length)
                        return false;

                    int N = fields1.Length;

                    for (int i = 0; i < N; i++)
                    {
                        HType ft1 = fields1[i].Type;
                        HType ft2 = fields2[i].Type;

                        if (_areTypesEquivalent(ft1, ft2))
                            return false;
                    }
                    return true;
                }
                else if (t1 is HFunType && t2 is HFunType)
                {
                    HFunType ft1 = t1 as HFunType;
                    HFunType ft2 = t2 as HFunType;

                    if (!_areTypesEquivalent(ft1.ResType, ft2.ResType))
                        return false;

                    HType[] typeArray1 = ft1.ParamTypes;
                    HType[] typeArray2 = ft2.ParamTypes;

                    if (typeArray1.Length != typeArray2.Length)
                        return false;

                    int N = typeArray1.Length;

                    for (int i = 0; i < N; i++)
                        if (!_areTypesEquivalent(typeArray1[i], typeArray2[i]))
                            return false;
                }
                return false;
            }
        }

        static public bool _areTypesEquivalent(HBinaryExpr e)
        {
            return _areTypesEquivalent(e.Left.Type, e.Right.Type);
        }

        static private override void _checkFunParams(HCallNonVoidFunExpr t)
        {
            Debug.Assert(t.ToBeCalled.Type is HFunType);
            HFunType funType = t.ToBeCalled.Type as HFunType;
            Debug.Assert(t.Params.Length == funType.ParamTypes.Length);

            int N = t.Params.Length;

            for (int i = 0; i < N; i++)
            {
                HType type = funType.ParamTypes[i];
                HExpr expr = t.Params[i];

                Debug.Assert(_areTypesEquivalent(type, expr.Type));
            }
        }

        #endregion
    }

    #endregion

}