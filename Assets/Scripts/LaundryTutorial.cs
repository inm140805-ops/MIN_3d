using System;
using System.Linq;
using TMPro;
using UnityEngine;

// Observes real gameplay only. Never grants stock, completes orders, or changes the clock.
public class LaundryTutorial : MonoBehaviour
{
    const string SaveKey = "Laundry.Tutorial.v1";
    const int Opened = 1, Washed = 2, Collected = 4, Stocked = 8, Finished = 16;
    [Serializable] class Progress { public int learned; public bool hidden; }
    [SerializeField] private GameObject panel, arrowRoot;
    [SerializeField] private RectTransform panelRect, arrow, arrowCanvas;
    [SerializeField] private TMP_Text heading, message, footer, arrowLabel, helpHint;
    [SerializeField] private Transform opening, terminalPoint, deliveryPoint, supplyPoint;
    [SerializeField] private Transform terminalVisual, deliveryVisual, supplyVisual;
    [SerializeField] private LaundryWorkflow.Station[] stations;
    [SerializeField] private RectTransform orderButton, closeComputer, suppliesTab, clothingTab, decreaseQuantity;
    [SerializeField] private RectTransform[] stockRows;
    [SerializeField] private Vector3[] routeNodes;
    [SerializeField] private int[] routeEdges;
    private ShopEconomy economy;
    private ShopComputer computer;
    private CoreLoopManager loop;
    private Player player;
    private Grab grab;
    private BoxManager boxes;
    private Progress progress;
    private bool persistent, guiding;
    private float nextUpdate;
    private NPCUI[] customers = new NPCUI[0];
    private NPCUI collectionCustomer;
    private Vector3? destination;
    private RectTransform uiTarget;
    private Vector3 arrowPoint;
    private string targetName;
    private int requestedCatalog;
    public string StepKey { get; private set; }
    public bool IsGuiding => guiding;
    public int Learned => progress == null ? 0 : progress.learned;
    public Vector3? Destination => destination;

    private void Start()
    {
        economy=FindAnyObjectByType<ShopEconomy>(); computer=FindAnyObjectByType<ShopComputer>();
        loop=FindAnyObjectByType<CoreLoopManager>(); player=FindAnyObjectByType<Player>();
        grab=player.GetComponent<Grab>(); boxes=FindAnyObjectByType<BoxManager>();
        persistent=true;
#if UNITY_EDITOR
        persistent=string.IsNullOrEmpty(UnityEditor.SessionState.GetString("LaundryQA.ActiveSession", ""));
#endif
        progress=new Progress();
        if(persistent && PlayerPrefs.HasKey(SaveKey))
            try { progress=JsonUtility.FromJson<Progress>(PlayerPrefs.GetString(SaveKey)) ?? new Progress(); } catch { progress=new Progress(); }
        progress.learned &= 31;
        guiding=!progress.hidden && (progress.learned & Finished)==0;
        Tick();
    }
    private void Save() { if(persistent) { PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(progress)); PlayerPrefs.Save(); } }
    private void Mark(int bit) { if((progress.learned&bit)!=0)return; progress.learned|=bit;Save(); }
    private void Update()
    {
        if(progress==null)return;
        if(GameInput.GetKeyDown(KeyCode.F1))
        {
            guiding=!guiding; progress.hidden=!guiding;
            if(guiding && (progress.learned&Finished)!=0)progress.learned=0;
            Save(); nextUpdate=0;
        }
        bool paused=Time.timeScale<=0 && !computer.IsOpen && !loop.IsResultOpen;
        helpHint.gameObject.SetActive(!paused && !computer.IsOpen && !loop.IsResultOpen && !guiding);
        panel.SetActive(guiding && !paused); arrowRoot.SetActive(guiding && !paused);
        if(!guiding || paused)return;
        if(Time.unscaledTime>=nextUpdate){nextUpdate=Time.unscaledTime+.15f;Tick();}
        if(Done(Finished) && !loop.IsResultOpen){guiding=false;panel.SetActive(false);arrowRoot.SetActive(false);helpHint.gameObject.SetActive(true);return;}
        PlaceArrow();
    }
    private bool Done(int bit) => (progress.learned&bit)!=0;
    private void Tick()
    {
        customers=FindObjectsByType<NPCUI>(FindObjectsSortMode.None);
        if(loop.isOpen)Mark(Opened);
        foreach(var n in customers)if(n.orderComplete)Mark(n.Purpose==NPCUI.VisitPurpose.DropOff?Washed:Collected);
        if(economy.Operations.delivery!=null && economy.Operations.delivery.Complete)Mark(Stocked);
        if(economy.Operations.tickets.Any(t=>t.stage==LaundryStage.Returned))Mark(Washed);
        destination=null;uiTarget=null;
        if(loop.IsResultOpen)
        {
            var button=loop.reportFrame.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(b=>b.interactable);
            if((progress.learned&15)==15)Mark(Finished);
            Tell("day-result","하루 정산","매출·성공·실패를 확인하세요. 다음 날에도 재고와 설비는 유지됩니다.\n버튼으로 다음 영업을 시작하거나 다시 도전하세요.",null,"정산");
            if(button)uiTarget=(RectTransform)button.transform;
        }
        else if(computer.IsOpen) ComputerGuide();
        else if(economy.Operations.delivery!=null && economy.Operations.delivery.carrying>0) DeliveryGuide();
        else if(economy.HeldTicket!=null) HeldGuide(economy.HeldTicket);
        else if(grab.itemID>=0) CollectionGuide(true);
        else
        {
            var ticket=economy.Operations.tickets.FirstOrDefault(t=>t.stage!=LaundryStage.Returned);
            if(ticket!=null) StationGuide(ticket);
            else if(economy.Operations.delivery!=null && !economy.Operations.delivery.Complete) DeliveryGuide();
            else if(!loop.isOpen) Tell("open","영업을 시작하세요","카운터의 영업 표지판에 가까이 가서 E를 누르세요.\n손님은 옷을 맡기거나 세탁된 옷을 찾으러 옵니다.",new Vector3(6.1f,.3f,12.65f),"영업 표지판");
            else if((progress.learned&15)==15 && player.money>=loop.needMoney)
                Tell("close-day","오늘 목표 달성!","영업 표지판에서 E를 눌러 정산하세요.\n컴퓨터의 설비 업그레이드는 다음 날에도 유지됩니다.",new Vector3(6.1f,.3f,12.65f),"영업 마감");
            else if(Done(Washed)&&Done(Collected)&&!Done(Stocked)&&CanOrder(0)) OrderGuide(0,"물품 배송도 경험해 보세요. 세제를 발주한 뒤 직접 입고합니다.");
            else
            {
                var drop=customers.FirstOrDefault(n=>n.IsWaitingForOrder && n.Purpose==NPCUI.VisitPurpose.DropOff && !n.HasPendingLaundry);
                var collect=customers.FirstOrDefault(n=>n.IsWaitingForOrder && n.Purpose==NPCUI.VisitPurpose.Collection);
                if(collect && (Done(Washed)||!drop)) CollectionGuide(false);
                else if(drop) Tell("accept","옷을 맡기러 온 손님","화살표가 가리키는 손님 앞에서 E를 누르세요.\n접수 번호가 붙은 옷을 받아 세탁→건조→포장합니다.",Service(drop),"세탁 접수 손님");
                else Tell("wait-customer","손님을 기다리세요","카운터로 오는 손님의 목적을 확인하세요.\n‘세탁 접수’는 작업 의뢰, ‘의류 수령’은 보관함의 옷을 찾아 주는 주문입니다.",new Vector3(4.8f,.3f,22.57f),"손님 카운터");
            }
        }
        Layout();
        if(destination.HasValue)arrowPoint=NextRoutePoint(player.transform.position,destination.Value);
    }
    private bool CanOrder(int catalog) => economy.CatalogSpace(catalog)>0 && (economy.Funds>=economy.CatalogUnitCost(catalog) || (catalog<3 && economy.Supply(catalog)==0));
    private void Tell(string key,string title,string body,Vector3? point,string label)
    {StepKey=key;heading.text=title;message.text=body;destination=point;targetName=label;}
    private Vector3 Service(NPCUI n) => new Vector3(4.8f,.3f,n.transform.position.z);
    private LaundryWorkflow.Station Station(LaundryTicket t) => stations.FirstOrDefault(s=>s.location==t.location && (s.location!=LaundryLocation.Washer || s.machine==t.machine));
    private void HeldGuide(LaundryTicket t)
    {
        if(t.stage==LaundryStage.Ready)
        {
            var owner=customers.FirstOrDefault(n=>n.LaundryTicketId==t.id && !n.orderComplete);
            Tell("return-laundry","의뢰 #"+t.id+" 돌려주기","이 번호의 손님 앞에서 E를 누르세요. 다른 손님에게는 반환할 수 없습니다.\n반환하면 매출과 운영자금을 받습니다.",owner?Service(owner):(Vector3?)null,"의뢰 #"+t.id+" 손님");return;
        }
        var location=t.stage==LaundryStage.Dirty?LaundryLocation.Washer:t.stage==LaundryStage.Wet?LaundryLocation.Dryer:LaundryLocation.Table;
        var station=stations.FirstOrDefault(s=>s.location==location && economy.AtStation(s.location,s.machine)==null);
        if(station==null)station=stations.FirstOrDefault(s=>s.location==LaundryLocation.Table && economy.AtStation(s.location,s.machine)==null);
        if(station==null && t.stage==LaundryStage.Wet)station=stations.FirstOrDefault(s=>s.location==LaundryLocation.Washer && economy.AtStation(s.location,s.machine)==null);
        if(station==null){Tell("station-busy","사용 중인 기계입니다","먼저 넣은 옷의 작업이 끝날 때까지 기다리세요.\n손에 든 옷을 정리대에 임시 보관하면 다른 작업을 할 수 있습니다.",null,"기계 사용 중");return;}
        if(station.location!=location){Tell("park-busy","사용 중인 기계입니다",station.title+"에서 E를 눌러 옷을 임시 보관하세요.\n사용하던 기계가 비면 이 옷을 꺼내 작업을 이어가세요.",station.usePoint.position,"임시 보관 · "+station.title);return;}
        int supply=t.stage==LaundryStage.Dirty?0:t.stage==LaundryStage.Wet?1:2;
        bool shortage=economy.Supply(supply)<t.quantity;
        Tell(shortage?"park-garment":"load-"+location.ToString().ToLower(),shortage?"소모품이 부족해요":station.title+"에 옷 넣기",
            shortage?"E로 옷을 임시 보관해 손을 비우세요.\n그다음 컴퓨터에서 "+ShopEconomy.SupplyNames[supply]+"를 발주하고 직접 입고합니다.":
            "화살표 위치에서 E를 누르세요. "+ShopEconomy.SupplyNames[supply]+" "+t.quantity+"개를 사용합니다.\n"+(location==LaundryLocation.Washer?"세탁은 12초. 내부 드럼이 돌면 작동 중입니다.":location==LaundryLocation.Dryer?"젖은 옷을 8초 동안 건조합니다.":"마른 옷을 3초 동안 정리하고 포장합니다."),station.usePoint.position,station.title);
    }
    private void StationGuide(LaundryTicket t)
    {
        var s=Station(t); if(s==null){Tell("recover","의뢰 확인","보관 중인 옷의 위치를 확인하고 번호에 맞춰 작업을 이어가세요.",null,"의뢰 #"+t.id);return;}
        bool working=t.stage==LaundryStage.Washing||t.stage==LaundryStage.Drying||t.stage==LaundryStage.Folding;
        int required=t.stage==LaundryStage.Dirty?0:t.stage==LaundryStage.Wet&&t.location==LaundryLocation.Dryer?1:t.stage==LaundryStage.Dry&&t.location==LaundryLocation.Table?2:-1;
        if(!working&&required>=0 && economy.Supply(required)<t.quantity)
        {if(economy.Operations.delivery!=null&&!economy.Operations.delivery.Complete)DeliveryGuide();else OrderGuide(required,ShopEconomy.SupplyNames[required]+"가 부족합니다. 입고 후 옷을 꺼냈다가 다시 넣으면 작업이 시작됩니다.");return;}
        Tell(working?"wait-"+t.stage.ToString().ToLower():"unload",working?LaundryWorkflow.StageName(t.stage):s.title+"에서 옷 꺼내기",
            working?"남은 시간 "+Mathf.Max(0,Mathf.CeilToInt((float)(t.readyAt-LaundryClock.Now)))+"초 · 작동 중에는 꺼낼 수 없습니다.\n완료 표시가 나오면 같은 기계에서 E를 누르세요.":
            "E를 눌러 의뢰 #"+t.id+"의 옷을 꺼내세요.\n"+(t.stage==LaundryStage.Wet?"다음은 건조기입니다.":t.stage==LaundryStage.Dry?"다음은 정리·포장대입니다.":t.stage==LaundryStage.Ready?"이제 같은 의뢰 번호의 손님에게 반환하세요.":"소모품을 채웠다면 옷을 다시 넣어 작업을 시작하세요."),s.usePoint.position,s.title);
    }
    private void CollectionGuide(bool carrying)
    {
        var recent=customers.Where(n=>n.IsWaitingForOrder&&n.Purpose==NPCUI.VisitPurpose.Collection)
            .OrderByDescending(n=>n.HasReadOrder).ThenByDescending(n=>n.OrderReadTime).FirstOrDefault();
        if(!collectionCustomer || !collectionCustomer.IsWaitingForOrder || (recent && recent.HasReadOrder))collectionCustomer=recent;
        if(carrying)
        {
            bool matches=collectionCustomer && collectionCustomer.AcceptedClothing.Contains(boxes.boxDatas[grab.itemID].Id);
            if(matches)Tell("return-collection","찾으러 온 손님에게 전달","화살표 손님 앞에서 E를 누르세요.\n대화를 아직 읽지 않았다면 주문을 확인한 뒤 E를 한 번 더 누르세요.",Service(collectionCustomer),"의류 수령 손님");
            else Tell("wrong-clothes","이 옷은 주문과 달라요","먼저 꺼냈던 보관함에서 E를 눌러 되돌려 놓으세요.\n틀린 옷을 전달해도 돈이나 물품은 사라지지 않습니다.",Shelf(grab.itemID),"원래 의류 보관함");
            return;
        }
        if(!collectionCustomer)return;
        if(!collectionCustomer.HasReadOrder){Tell("read-collection","옷을 찾으러 온 손님","손님 앞에서 E를 눌러 어떤 옷을 찾는지 읽으세요.\n이 손님의 옷은 이미 세탁되어 의류 보관함에 있습니다.",Service(collectionCustomer),"수령 주문 확인");return;}
        int item=Array.FindIndex(boxes.boxDatas,b=>collectionCustomer.AcceptedClothing.Contains(b.Id)&&economy.Stock(Array.IndexOf(boxes.boxDatas,b))>0);
        if(item<0){int missing=Array.FindIndex(boxes.boxDatas,b=>collectionCustomer.AcceptedClothing.Contains(b.Id));OrderGuide(missing+3,"찾는 의류가 품절입니다. 의류 재고 탭에서 발주하고 해당 보관함에 입고하세요.");return;}
        Tell("take-collection",boxes.boxDatas[item].BoxName+" 꺼내기","맞는 보관함에 가까이 가서 E를 누르세요.\n화살표가 가리키는 옷을 손님에게 전달합니다.",Shelf(item),boxes.boxDatas[item].BoxName+" 보관함");
    }
    private Vector3 Shelf(int item) {var p=boxes.StoragePositions[Mathf.Clamp(item,0,boxes.boxDatas.Length-1)].position;return new Vector3(11,.3f,Mathf.Min(p.z,28.3f));}
    private void OrderGuide(int catalog,string reason)
    {requestedCatalog=catalog;Tell("order","컴퓨터에서 물품 주문",reason+"\n가게 컴퓨터 앞에서 E를 누르세요. 주문은 60초 뒤 트럭으로 옵니다.",terminalPoint.position,"가게 컴퓨터");}
    private void DeliveryGuide()
    {
        var d=economy.Operations.delivery;if(d==null)return;
        if(d.carrying>0){Tell("store-delivery","보관함에 직접 입고","E를 눌러 운반한 물품을 보관하세요.\n박스를 열거나 물품을 들기만 해서는 재고가 늘지 않습니다.",economy.IsSupplyCatalog(d.catalog)?supplyPoint.position:Shelf(d.catalog-3),economy.IsSupplyCatalog(d.catalog)?"세탁용품 보관함":"주문한 의류 보관함");return;}
        if(LaundryClock.Now<d.dueAt+4){Tell("wait-delivery","배송 트럭을 기다리세요","도착까지 "+Mathf.Max(0,Mathf.CeilToInt((float)(d.dueAt-LaundryClock.Now)))+"초 · 정차 후 하역까지 기다리세요.\n도로에 서지 말고 화살표의 배송 구역에서 받으세요.",deliveryPoint.position,"배송 대기 구역");return;}
        Tell(d.opened?"take-delivery":"open-parcel",d.opened?"배송 물품 꺼내기":"배송 박스 열기",d.opened?"E로 한 번에 최대 3개를 꺼내세요.\n보관함에 입고한 뒤 남은 물품도 다시 가져오세요.":"박스 옆에서 E를 누르세요.\n열린 박스에서 E를 한 번 더 눌러 물품을 직접 꺼냅니다.",deliveryPoint.position,"배송 박스");
    }
    private void ComputerGuide()
    {
        var d=economy.Operations.delivery;
        if(d!=null&&(!d.Complete||LaundryClock.Now<d.dueAt+35))
        {Tell("computer-close","주문 접수 완료","ESC 또는 닫기를 누르세요. 배송 중에는 추가 주문이 제한됩니다.\n박스를 직접 받아 보관함에 입고하면 재고가 늘어납니다.",null,"닫기");uiTarget=closeComputer;return;}
        bool supplies=economy.IsSupplyCatalog(requestedCatalog);
        if(computer.ShowingSupplies!=supplies){Tell("computer-tab","주문할 물품 종류 선택",supplies?"세탁용품 탭에서 세제·건조 시트·포장재를 선택하세요.":"의류 재고 탭에서 찾는 옷을 선택하세요.",null,"물품 탭");uiTarget=supplies?suppliesTab:clothingTab;return;}
        if(computer.SelectedCatalog!=requestedCatalog){Tell("computer-item","필요한 물품 선택",LaundryWorkflow.Current.CatalogName(requestedCatalog)+" 항목을 누르세요. 숫자는 현재 재고와 보관 한도입니다.",null,"물품 선택");uiTarget=stockRows[supplies?Mathf.Min(requestedCatalog,3):requestedCatalog-3];return;}
        int unit=economy.CatalogUnitCost(requestedCatalog),space=economy.CatalogSpace(requestedCatalog);
        bool emergency=requestedCatalog<3&&economy.Supply(requestedCatalog)==0&&economy.Funds<unit;
        if(space<=0||(!emergency&&economy.Funds<unit))
        {Tell("computer-unavailable","지금은 발주할 수 없어요",space<=0?"보관함이 가득 찼습니다. 먼저 물품을 사용하세요. 창고 확장으로 한도를 늘릴 수도 있습니다.":"운영자금이 부족합니다. 손님에게 옷을 반환해 자금을 모으세요. 오늘 매출과 운영자금은 별도입니다.",null,"닫고 영업 계속");uiTarget=closeComputer;return;}
        if(computer.OrderQuantity>space||(!emergency&&computer.OrderQuantity*unit>economy.Funds)||(emergency&&computer.OrderQuantity>1))
        {Tell("computer-quantity","주문 수량 줄이기",emergency?"소모품이 없고 자금도 부족하면 1개를 무료 긴급 배송받을 수 있습니다. − 버튼으로 1개를 선택하세요.":"− 버튼으로 보관 공간과 자금에 맞게 수량을 줄이세요. + 버튼으로 늘릴 수 있습니다.",null,"수량 −");uiTarget=decreaseQuantity;return;}
        Tell("computer-order","발주 버튼 누르기","물품과 수량·가격을 확인하고 발주하세요. 금액은 한 번 차감됩니다.\n오른쪽 설비 업그레이드는 보관량·발주 단가·주문 수익을 개선합니다.",null,"발주 확정");uiTarget=orderButton;
    }
    private void Layout()
    {
        bool compact=computer.IsOpen;
        panelRect.anchorMin=panelRect.anchorMax=compact?new Vector2(.5f,0):new Vector2(0,1);
        panelRect.pivot=compact?new Vector2(.5f,0):new Vector2(0,1);
        panelRect.anchoredPosition=compact?new Vector2(0,8):new Vector2(32,-330);
        panelRect.sizeDelta=compact?new Vector2(1120,88):new Vector2(540,230);
        heading.transform.parent.gameObject.SetActive(!compact);footer.gameObject.SetActive(!compact);
        message.rectTransform.anchorMin=Vector2.zero;message.rectTransform.anchorMax=Vector2.one;
        message.rectTransform.offsetMin=compact?new Vector2(18,6):new Vector2(18,45);
        message.rectTransform.offsetMax=compact?new Vector2(-18,-6):new Vector2(-18,-53);
        message.fontSize=compact?23:25;
        int count=new[]{Opened,Washed,Collected,Stocked}.Count(Done);
        footer.text="운영 실습 "+count+" / 4    ·    F1 안내 끄기";
        panel.transform.parent.SetAsLastSibling();
    }
    private void PlaceArrow()
    {
        if(!destination.HasValue && !uiTarget){arrowRoot.SetActive(false);return;}
        Vector3 screen;bool behind=false;
        if(uiTarget){screen=RectTransformUtility.WorldToScreenPoint(null,uiTarget.TransformPoint(uiTarget.rect.center));}
        else {screen=Camera.main.WorldToScreenPoint(VisualPoint());behind=screen.z<0;}
        RectTransformUtility.ScreenPointToLocalPointInRectangle(arrowCanvas,screen,null,out var point);
        var safe=new Rect(arrowCanvas.rect.xMin+95,arrowCanvas.rect.yMin+145,arrowCanvas.rect.width-190,arrowCanvas.rect.height-320);
        bool outside=behind||!safe.Contains(point);
        if(behind)point=-point;
        if(outside && point.sqrMagnitude<1)point=Vector2.down;
        float angle=180;
        if(outside){angle=Mathf.Atan2(point.y,point.x)*Mathf.Rad2Deg-90;float ratio=Mathf.Min((safe.width*.5f)/Mathf.Max(1,Mathf.Abs(point.x)),(safe.height*.5f)/Mathf.Max(1,Mathf.Abs(point.y)));point=point*ratio;}
        else point+=Vector2.up*(68+Mathf.Sin(Time.unscaledTime*4)*5);
        point.x=Mathf.Clamp(point.x,safe.xMin,safe.xMax);point.y=Mathf.Clamp(point.y,safe.yMin,safe.yMax);
        arrowRoot.GetComponent<RectTransform>().anchoredPosition=point;arrow.localRotation=Quaternion.Euler(0,0,angle);
        arrowLabel.gameObject.SetActive(!uiTarget);
        float labelX=Mathf.Clamp(point.x,arrowCanvas.rect.xMin+165,arrowCanvas.rect.xMax-165)-point.x;
        arrowLabel.rectTransform.anchoredPosition=new Vector2(labelX,-77);
        bool route=destination.HasValue&&Vector3.Distance(arrowPoint,destination.Value)>.6f;
        arrowLabel.text=(behind?"뒤돌아보기 · ":outside?"방향 · ":route?"통로 · ":"")+targetName;
        if(destination.HasValue)arrowLabel.text+="\n"+Mathf.CeilToInt(Vector3.Distance(player.transform.position,destination.Value))+"m";
    }
    private Vector3 VisualPoint()
    {
        if(!destination.HasValue || Vector3.Distance(player.transform.position,destination.Value)>2.2f)
            return arrowPoint+Vector3.up*.95f;
        Vector3 goal=destination.Value;
        foreach(var s in stations)if(Vector3.Distance(goal,s.usePoint.position)<.2f)
            return s.drum?s.drum.position:s.status.transform.position;
        if(Vector3.Distance(goal,terminalPoint.position)<.2f)return terminalVisual.position;
        if(Vector3.Distance(goal,supplyPoint.position)<.2f)return supplyVisual.position;
        if(Vector3.Distance(goal,deliveryPoint.position)<.2f)return deliveryVisual.position+Vector3.up*.65f;
        if(StepKey=="open"||StepKey=="close-day")return opening.position;
        for(int i=0;i<boxes.StoragePositions.Length;i++)if(Vector3.Distance(goal,Shelf(i))<.2f)return boxes.StoragePositions[i].position+Vector3.up*.5f;
        foreach(var n in customers)if(n && Vector3.Distance(goal,Service(n))<.2f)return n.transform.position+Vector3.up*1.5f;
        return arrowPoint+Vector3.up*.95f;
    }
    private bool Clear(Vector3 from,Vector3 to)
    {
        from.y=.3f;to.y=.3f;var delta=to-from;if(delta.magnitude<.1f)return true;
        foreach(var hit in Physics.CapsuleCastAll(from+Vector3.up*.99f,from+Vector3.up*1.57f,.89f,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
            if(!hit.collider.transform.IsChildOf(player.transform)&&hit.collider.bounds.max.y>.42f&&hit.collider.GetComponentInParent<NPCUI>()==null)return false;
        return true;
    }
    private Vector3 NextRoutePoint(Vector3 from,Vector3 goal)
    {
        if(Clear(from,goal))return goal;
        int n=routeNodes.Length;var distance=new float[n];var first=new int[n];var visited=new bool[n];
        for(int i=0;i<n;i++){distance[i]=Clear(from,routeNodes[i])?Vector3.Distance(from,routeNodes[i]):float.PositiveInfinity;first[i]=distance[i]<.6f?-1:i;}
        for(int iteration=0;iteration<n;iteration++)
        {
            int a=-1;for(int i=0;i<n;i++)if(!visited[i]&&(a<0||distance[i]<distance[a]))a=i;
            if(a<0||float.IsPositiveInfinity(distance[a]))break;visited[a]=true;
            for(int e=0;e+1<routeEdges.Length;e+=2){int b=routeEdges[e]==a?routeEdges[e+1]:routeEdges[e+1]==a?routeEdges[e]:-1;if(b<0)continue;float cost=distance[a]+Vector3.Distance(routeNodes[a],routeNodes[b]);if(cost<distance[b]){distance[b]=cost;first[b]=first[a]<0?b:first[a];}}
        }
        float best=float.PositiveInfinity;int chosen=-1;
        for(int i=0;i<n;i++){float cost=distance[i]+Vector3.Distance(routeNodes[i],goal);if(cost<best&&Clear(routeNodes[i],goal)){best=cost;chosen=first[i];}}
        return chosen>=0?routeNodes[chosen]:goal;
    }
}
