
//// DataTables initialisation

$('#tableUser').dataTable({
   
    columnControl: {
        target: 1,
        content: ['search']
    },
    "autoWidth": true,
    "responsive": true,

    layout: {
        topEnd: {
            search: {
                placeholder: 'Search'
            }
        },
        topStart: {
            pageLength: {
                menu: [10, 25, 50, 100, 200, 400, 1000]
            }
        },
        bottomEnd: {
            paging: {
                buttons: 6
            }
        }
    },
    language: {
        "autoWidth": true,
        "responsive": true,
        search: '', // Customize the search input label
        lengthMenu: ' _MENU_ ', // Customize the "Show X entries" label
        info: 'Show _START_ To _END_ From _TOTAL_ entries', // Customize the info text
        zeroRecords: 'No records found', // No records message
        paginate: {
            first: 'Frist',
            previous: 'Next',
            next: 'Prev',
            last: 'Last'
        }
    }
});

$('#tablePermissions').dataTable({

    columnControl: {
        target: 1,
        content: ['search']
    },
    "autoWidth": true,
    "responsive": true,
    "pageLength": 200,
    layout: {
        topStart: {
            search: {
                placeholder: ''
            }
        },
        topEnd: {
            pageLength: {
                menu: [10, 25, 50, 100, 200, 400, 1000]
            }
        },
        bottomEnd: {
            paging: {
                buttons: 6
            }
        }
    },
    language: {
        "autoWidth": true,
        "responsive": true,
        search: 'بحث', // Customize the search input label
        lengthMenu: ' _MENU_ ', // Customize the "Show X entries" label
        info: 'إظهار _START_ إلى _END_ من أصل _TOTAL_ سجل', // Customize the info text
        zeroRecords: 'لم يعثر على أي سجل', // No records message
        paginate: {
            first: 'الأول',
            previous: 'السابق',
            next: 'التالي',
            last: 'الأخير'
        }
    }
});

//User Methods
Edit = (id, name,passUser,passSign) => {
    document.getElementById("title").innerHTML = lbTitleEdit;
    document.getElementById("btnSave").value = lbEdit;
    document.getElementById("UserId").value = id;
    document.getElementById("UserName").value = name;
    document.getElementById("passUser").value = passUser;
    document.getElementById("passSign").value = passSign;
}
Rest = () => {
    document.getElementById("title").innerHTML = lbAddNewUser;
    document.getElementById("btnSave").value = lbbtnSave;
    document.getElementById("UserId").value = "";
    document.getElementById("UserName").value = "";

}
DeleteUser = (id) => {
    Swal.fire({
        title: lbTitleMsgDelete,
        text: lbTextMsgDelete,
        icon: 'error',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#57595B',
        confirmButtonText: lbconfirmButtonText,
        cancelButtonText: lbcancelButtonText,

    }).then((result) => {
        if (result.isConfirmed) {
            window.location.href = `/Users/DeleteUser?Id=${id}`;
        }
    })
}
ManageRoles = (id) => {
    document.getElementById("usrId").value = id;
}

//Supplier Methods
EditSupplier = (id, name, phone,adress,email) => {
    document.getElementById("title").innerHTML = lbTitleEdit;
    document.getElementById("btnSave").value = lbEdit;
    document.getElementById("suppId").value = id;
    document.getElementById("suppName").value = name;
    document.getElementById("suppPhone").value = phone;
    document.getElementById("suppAdress").value = adress;
    document.getElementById("suppEmail").value = email;
}
ResetSupplier = () => {
    document.getElementById("title").innerHTML = lbAddNewRole;
    document.getElementById("btnSave").value = lbbtnSave;
    document.getElementById("suppName").value = "";
    document.getElementById("suppPhone").value = "";
    document.getElementById("suppAdress").value = "";
    document.getElementById("suppEmail").value = "";
}
DeleteSupplier = (id) => {
    Swal.fire({
        title: lbTitleMsgDelete,
        text: lbTextMsgDelete,
        icon: 'error',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#57595B',
        confirmButtonText: lbconfirmButtonText,
        cancelButtonText: lbcancelButtonText,

    }).then((result) => {
        if (result.isConfirmed) {
            window.location.href = `/Suppliers/Delete?Id=${id}`;
        }
    })
}

//Category Methods
EditCategory = (id, name, details) => {
    document.getElementById("title").innerHTML = lbTitleEdit;
    document.getElementById("btnSave").value = lbEdit;
    document.getElementById("catId").value = id;
    document.getElementById("catName").value = name;
    document.getElementById("catDetails").value = details;
   
}
ResetCategory = () => {
    document.getElementById("title").innerHTML = lbAddNewRole;
    document.getElementById("btnSave").value = lbbtnSave;
    document.getElementById("catName").value = "";
    document.getElementById("catDetails").value = "";
}
DeleteCategory = (id) =>{
    Swal.fire({
        title: lbTitleMsgDelete,
        text: lbTextMsgDelete,
        icon: 'error',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#57595B',
        confirmButtonText: lbconfirmButtonText,
        cancelButtonText: lbcancelButtonText,

    }).then((result) => {
        if (result.isConfirmed) {
            window.location.href = `/Categories/Delete?Id=${id}`;
        }
    })
}

//ManufacuterCompany Methods
EditManufacuterCompany = (id, name,shName, details) => {
    document.getElementById("title").innerHTML = lbTitleEdit;
    document.getElementById("btnSave").value = lbEdit;
    document.getElementById("catId").value = id;
    document.getElementById("catName").value = name;
    document.getElementById("catShCutName").value = shName;
    document.getElementById("catDetails").value = details;

}
ResetManufacuterCompany = () => {
    document.getElementById("title").innerHTML = lbAddNewRole;
    document.getElementById("btnSave").value = lbbtnSave;
    document.getElementById("catName").value = "";
    document.getElementById("catShCutName").value = "";
    document.getElementById("catDetails").value = "";
}
DeleteManufacuterCompany = (id) => {
    Swal.fire({
        title: lbTitleMsgDelete,
        text: lbTextMsgDelete,
        icon: 'error',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#57595B',
        confirmButtonText: lbconfirmButtonText,
        cancelButtonText: lbcancelButtonText,

    }).then((result) => {
        if (result.isConfirmed) {
            window.location.href = `/ManufacuterCompanies/Delete?Id=${id}`;
        }
    })
}

//Location Methods
EditLocation = (id, locRoomType, locRoomNum, locCaseNum, locSelfNum, locRackNum, locBoxNum, locTubeNum) => {
    document.getElementById("title").innerHTML = lbTitleEdit;
    document.getElementById("btnSave").value = lbEdit;
    document.getElementById("locId").value = id;
    //document.getElementById("locName").value = name;
    //document.getElementById("locDetails").value = shName;

    document.getElementById("locRoomType").selectedIndex = locRoomType;
    document.getElementById("locRoomNum").selectedIndex = locRoomNum;
    document.getElementById("locCaseNum").selectedIndex = locCaseNum;
    document.getElementById("locSelfNum").selectedIndex = locSelfNum;
    document.getElementById("locRackNum").selectedIndex = locRackNum;
    document.getElementById("locBoxNum").selectedIndex = locBoxNum;
    document.getElementById("locTubeNum").selectedIndex = locTubeNum;
    
}
ResetLocation = () => {
    document.getElementById("title").innerHTML = lbAddNewRole;
    document.getElementById("btnSave").value = lbbtnSave;
    //document.getElementById("locName").value = "";
    //document.getElementById("locDetails").value = "";
    document.getElementById("locRoomType").selectedIndex = 0;
    document.getElementById("locRoomNum").selectedIndex = 0;
    document.getElementById("locCaseNum").selectedIndex = 0;
    document.getElementById("locSelfNum").selectedIndex = 0;
    document.getElementById("locRackNum").selectedIndex = 0;
    document.getElementById("locBoxNum").selectedIndex = 0;
    document.getElementById("locTubeNum").selectedIndex = 0;
    GetStorageTypes();
}
DeleteLocation = (id) => {
    Swal.fire({
        title: lbTitleMsgDelete,
        text: lbTextMsgDelete,
        icon: 'error',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#57595B',
        confirmButtonText: lbconfirmButtonText,
        cancelButtonText: lbcancelButtonText,

    }).then((result) => {
        if (result.isConfirmed) {
            window.location.href = `/Locations/Delete?Id=${id}`;
        }
    })
}
GetStorageTypes = () => {
    var roomType = document.getElementById("locRoomType");
    //var caseName = document.getElementById("locCase");
    //var caseNum = document.getElementById("locCase");
    
    if (roomType.selectedIndex == 0) { // reset
        document.getElementById("roomNum").style.display = "none";
        document.getElementById("caseNum").style.display = "none";
        document.getElementById("shelfNum").style.display = "none";
        document.getElementById("rackNum").style.display = "none";
        document.getElementById("boxNum").style.display = "none";
        document.getElementById("tubeNum").style.display = "none";
    }
    else if (roomType.selectedIndex == 1) { // normal
        document.getElementById("roomNum").style.display = "flex";
        document.getElementById("caseNum").style.display = "flex";
        document.getElementById("shelfNum").style.display = "flex";
        document.getElementById("rackNum").style.display = "none";
        document.getElementById("boxNum").style.display = "none";
        document.getElementById("tubeNum").style.display = "none";
    }
    else if (roomType.selectedIndex == 2) { //chill
        document.getElementById("roomNum").style.display = "flex";
        document.getElementById("caseNum").style.display = "flex";
        document.getElementById("shelfNum").style.display = "flex";
        document.getElementById("rackNum").style.display = "flex";
        document.getElementById("boxNum").style.display = "none";
        document.getElementById("tubeNum").style.display = "none";
    }
    else if (roomType.selectedIndex == 3) { // freez
        document.getElementById("roomNum").style.display = "flex";
        document.getElementById("caseNum").style.display = "flex";
        document.getElementById("shelfNum").style.display = "flex";
        document.getElementById("rackNum").style.display = "flex";
        document.getElementById("boxNum").style.display = "flex";
        document.getElementById("tubeNum").style.display = "flex";
    }
    else { // reset
        document.getElementById("roomNum").style.display = "none";
        document.getElementById("caseNum").style.display = "none";
        document.getElementById("shelfNum").style.display = "none";
        document.getElementById("rackNum").style.display = "none";
        document.getElementById("boxNum").style.display = "none";
        document.getElementById("tubeNum").style.display = "none";
    }
}

//ManufacuterCompany Methods
EditLot = (id, name, shName, details) => {
    document.getElementById("title").innerHTML = lbTitleEdit;
    document.getElementById("btnSave").value = lbEdit;
    

}
ResetLot = () => {
    document.getElementById("title").innerHTML = lbAddNewRole;
    document.getElementById("btnSave").value = lbbtnSave;
    
}
DeleteLot = (id) => {
    Swal.fire({
        title: lbTitleMsgDelete,
        text: lbTextMsgDelete,
        icon: 'error',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#57595B',
        confirmButtonText: lbconfirmButtonText,
        cancelButtonText: lbcancelButtonText,

    }).then((result) => {
        if (result.isConfirmed) {
            window.location.href = `/Lots/Delete?Id=${id}`;
        }
    })
}
