
//// DataTables initialisation

$('#tableUser').dataTable({
   
    columnControl: {
        target: 1,
        content: ['search']
    },
    "autoWidth": true,
    "responsive": true,

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

function DeleteUser(id) {
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

ManageRoles = (id) => {
    document.getElementById("usrId").value = id;
}

EditSupplier = (id, name, phone,adress,email) => {
    document.getElementById("title").innerHTML = lbTitleEdit;
    document.getElementById("btnSave").value = lbEdit;
    document.getElementById("suppId").value = id;
    document.getElementById("suppName").value = name;
    document.getElementById("suppPhone").value = phone;
    document.getElementById("suppAdress").value = adress;
    document.getElementById("suppEmail").value = email;
}

function DeleteSupplier(id) {
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
EditCategory = (id, name,  details) => {
    document.getElementById("title").innerHTML = lbTitleEdit;
    document.getElementById("btnSave").value = lbEdit;
    document.getElementById("catId").value = id;
    document.getElementById("catName").value = name;
    document.getElementById("catDetails").value = details;
   
}
function DeleteCategory(id) {
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

EditManufacuterCompany = (id, name,shName, details) => {
    document.getElementById("title").innerHTML = lbTitleEdit;
    document.getElementById("btnSave").value = lbEdit;
    document.getElementById("catId").value = id;
    document.getElementById("catName").value = name;
    document.getElementById("catShCutName").value = shName;
    document.getElementById("catDetails").value = details;

}
function DeleteManufacuterCompany(id) {
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